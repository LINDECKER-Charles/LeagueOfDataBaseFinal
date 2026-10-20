# ADR 0002 — Un seul hôte ASP.NET Core, qui absorbe les services Go

- **Statut** : acceptée — 2026-09-24
- **Remplace** : `go/fetcher` (passerelle d'egress) et `go/api` (API publique payante)
- **Amendement** : le 2026-10-20, un second hôte sortant, le stockage objet des pages
  pré-rendues, derrière son propre client nommé (voir § « Amendement du 2026-10-20 »).

## Contexte

Le backend actuel est réparti sur trois processus et trois langages :

- **PHP-FPM** ne sait ni paralléliser des requêtes HTTP sortantes à bas coût, ni garder
  un état entre deux requêtes. Le fetch Data Dragon a donc été confié à `go-fetcher`,
  au prix de :
  - corps renvoyés en base64 (+33 %) dans une enveloppe JSON ;
  - réponse seulement une fois tout le lot terminé ;
  - décodage complet en mémoire côté PHP ;
  - trois plafonds à synchroniser (1 Mio de requête, 512 URLs côté Go, 200 par lot côté
    PHP).
- **`go-api`** porte l'état longue durée de l'API publique : cache de clés, token bucket,
  comptage mensuel en mémoire, métrage vidé en base. Mais il lit les tables et la
  disposition du stockage écrites par PHP, décrites dans un `schema.sql` à tenir à la
  main. Une clé révoquée reste valide jusqu'à 60 s.
- Le même contrat est écrit dans deux ou trois langages (format de clé `lodb_`, regex de
  version, chemins de stockage, schéma) — cf. [`heritage.md`](../heritage.md) J2.

## Décision

**Un seul hôte `LoDb.Api`** (monolithe modulaire) qui sert :

| Surface | Rôle |
|---|---|
| `/api/…` | API de l'app (web SSR, desktop, Android) : catalogue, recherche, comptes, builds |
| `/v1/…` | API publique payante : même contrat qu'aujourd'hui (clés, quotas, crédits, enveloppe `{error:{code,message}}`) |
| `/webhooks/stripe` | webhooks signés, idempotents par `event.id` |
| workers | tâches de fond ([ADR 0003](0003-ingestion-proactive-et-taches-de-fond.md)) |

Modules internes séparés par dossier et par `IServiceCollection` d'extension (catalogue,
ingestion, comptes, builds, API publique, facturation, admin) ; aucune dépendance
circulaire, le domaine (`LoDb.Domain`) reste pur.

### Egress Data Dragon / CommunityDragon

Il passe dans le processus, avec les garanties que `go-fetcher` avait durement acquises :

- `IHttpClientFactory`, client nommé `ddragon`, `SocketsHttpHandler` en **HTTP/1.1**
  (le CDN d'images ne parle pas h2) avec `MaxConnectionsPerServer` aligné sur la
  concurrence de fetch.
- **`DelegatingHandler` d'allow-list** : https uniquement, hôtes
  `ddragon.leagueoflegends.com` et `raw.communitydragon.org`. Les redirections sont
  suivies à la main, et **chaque saut est re-vérifié** (`AllowAutoRedirect = false`).
- **Plafond de taille** de réponse, avec une erreur explicite (jamais une troncature).
- Résilience `Microsoft.Extensions.Http.Resilience` : timeout, retry exponentiel sur
  5xx/timeout, circuit breaker.
- **403/404 = absence définitive** (typée, jamais une erreur), **5xx/timeout =
  transitoire** (remonte, n'est jamais persisté).
- Streaming des corps (`ResponseHeadersRead`) : plus de base64, plus de lot bloquant.

L'allow-list devient une **politique** dans le code, testée. Un conteneur d'egress
isolé réseau ne se justifierait que si l'isolation devenait une exigence de sécurité
explicite ; ce n'est pas le cas aujourd'hui.

### API publique `/v1`

- Authentification par clé (`Authorization: Bearer` ou `X-Api-Key`), hash SHA-256 seul
  stocké — format `lodb_` + 40 hex conservé : les clés existantes restent valides.
- Rate limiting natif (`System.Threading.RateLimiting`, politique par clé) avec en-têtes
  `X-RateLimit-*`.
- Cache de clés en mémoire (`HybridCache`) **invalidé à la révocation** : même processus,
  donc révocation immédiate (au lieu de 60 s).
- Métrage : `Channel<T>` en mémoire, vidé par lot en base (upsert `api_usage`).
- Quotas : plan d'abord, puis crédits (décrément atomique en SQL), comme aujourd'hui.

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| Garder `go-fetcher` en passerelle | Le besoin d'origine (paralléliser depuis PHP) disparaît ; il resterait un aller-retour réseau et un 3ᵉ langage |
| Garder `go-api` séparé | Double lecture du même schéma, `schema.sql` à tenir à la main, révocation différée ; aucun gain de perf mesurable face à Kestrel |
| Microservices .NET (un service par module) | Complexité de déploiement et de contrats sans besoin de scaling différencié |

## Conséquences

- **+** Un seul processus à déployer, observer et dimensionner côté backend.
- **+** Entités EF Core partagées : plus de `schema.sql` à tenir à côté des migrations.
- **+** Fin des contrats dupliqués PHP ↔ Go (plafonds, format de clé, layout de stockage).
- **−** L'API publique partage les ressources de l'app. Si elle devait scaler à part, le
  module `/v1` peut être extrait tel quel en processus distinct (même solution, même
  code), derrière une route nginx.
- **Garde-fou** : les ports d'origine (`/v1/…`, en-têtes, codes d'erreur) sont couverts
  par des tests de contrat avant la bascule, pour ne casser aucun client payant.

## Amendement du 2026-10-20 — le stockage objet des pages pré-rendues

Le catalogue pré-rendu ([ADR 0011](0011-catalogue-pre-rendu-et-cdn.md)) écrit chaque page
d'ancien patch dans un stockage objet Scaleway. C'est un second hôte sortant, qui ne passe
pas par l'egress Data Dragon :

- **Client nommé `page-store`**, à part de `ddragon`. Son allow-list ne contient que l'hôte
  de `LoDb__Prerender__Endpoint` (`s3.fr-par.scw.cloud`), en https ; `AllowInsecureEndpoint`
  n'admet le http qu'en développement (MinIO). `LoDb:Egress:AllowedHosts` n'est jamais
  élargi : les deux politiques restent séparées et testées à part.
- Un `DelegatingHandler` épingle l'hôte, sur le modèle d'`UmamiHostHandler` ; aucune
  redirection suivie, aucun cookie, `AutomaticDecompression = None` (le corps est déjà en
  gzip et doit le rester).
- Seul le `PUT` est signé en SigV4. La clé est d'écriture seule : elle ne liste, ne lit ni
  ne supprime, et une lecture signée (`HEAD`, `GET`) répondrait toujours 403. Une
  vérification d'existence est anonyme, depuis l'adresse du VPS, ou n'existe pas : une
  écriture conditionnelle (`If-None-Match: *`, sur `build/`) qui répond 412 veut dire
  « déjà là ». La clé et son secret ne sont jamais journalisés.
- Les erreurs ne suivent **pas** la règle de l'egress, qui vaut pour une lecture. Une
  **401, 403 ou 404 (`NoSuchBucket`) sur un `PUT` est une erreur de configuration** (clé
  tournée, politique sans l'application d'écriture, stockage absent) : typée et fatale, elle
  met la passe en pause avec une ligne de journal, et la page n'est comptée ni écrite ni
  sautée. **5xx, 429 (freinage de Scaleway) et délai dépassé = transitoires**, typés,
  jamais persistés. Toute autre 4xx sur le `PUT` d'une page la compte en échec, sans nouvel
  essai ni pause ; une 409 sur le `PUT` conditionnel d'un bundle est retentée.
- Un changement d'adresse du VPS ne fait **jamais** échouer un `PUT` : l'écriture est
  autorisée par l'`application_id`, sans condition d'adresse. Seule la lecture anonyme en
  dépend : le changement ne se voit qu'aux lectures, quand nginx reçoit 403 et sert le SSR
  (`X-LoDb-Source: ssr`).
- Options vides = client et tâche en sommeil : `Add*` ne fait aucune I/O et ne lève rien.

Les appels de la passe au SSR (`http://lodb-web-ssr:4000`) restent sur le réseau de la pile :
ce n'est pas de l'egress.
