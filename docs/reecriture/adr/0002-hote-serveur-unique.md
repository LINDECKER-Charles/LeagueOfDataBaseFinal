# ADR 0002 — Un seul hôte ASP.NET Core, qui absorbe les services Go

- **Statut** : acceptée — 2026-09-24
- **Remplace** : `go/fetcher` (passerelle d'egress) et `go/api` (API publique payante)

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
