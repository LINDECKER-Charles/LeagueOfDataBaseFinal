# ADR 0010 — Observabilité, tests et CI

- **Statut** : acceptée — 2026-09-24
- **S'appuie sur** : la chaîne de logs existante vers Grafana
  ([`observabilite.md`](../../guides/observabilite.md)), la convention de journalisation
  ([`logging.md`](../../guides/logging.md))

## Contexte

L'observabilité actuelle a été conquise contre le runtime PHP :

- `display_errors` actif par défaut ;
- lignes JSON découpées par la limite de log de FPM (`log_limit`) ;
- access log FPM qui noyait la sortie standard ;
- handler Monolog `fingers_crossed` qui jetait 14 appels de log sur 17 ;
- sonde de santé qui ne prouvait que la présence du binaire.

Il reste des trous :

- aucune corrélation par identifiant de requête entre nginx, PHP et Go ;
- aucune métrique applicative, aucune métrique du pool FPM ;
- aucune révision de déploiement dans les logs.

La CI, elle, a d'autres manques :

- ni Postgres, ni stockage, ni go-fetcher ;
- les tests fonctionnels **se sautent** quand les datasets manquent ;
- côté front, ni typecheck ni build en CI ;
- le même contrat est écrit en PHP, TS et Go.

## Décision

### Logs

- `Microsoft.Extensions.Logging` avec le **formateur console JSON, une ligne par
  enregistrement**, sur stdout. Le driver `json-file` reste imposé : l'invariant infra
  ne change pas.
- La convention de [`logging.md`](../../guides/logging.md) est portée telle quelle :
  - clé d'événement `domaine.sujet.resultat`, portée par `EventId.Name` et des messages
    à gabarit ;
  - contexte structuré ; exception passée en objet ;
  - **aucune clé de contexte `error`** (le collecteur devine le niveau par regex) ;
  - aucune donnée personnelle, uniquement des identifiants internes ;
  - une ligne de synthèse par lot.
- Chaque ligne porte le **`trace_id`** : la corrélation nginx → SSR → API → Postgres
  devient native.
- Le serveur SSR Node journalise en JSON (pino), avec la même convention.

### Métriques et traces : OpenTelemetry

- **Métriques** : instrumentations ASP.NET Core, `HttpClient`, runtime .NET, EF Core,
  plus des métriques métier :
  - profondeur et durée des files d'ingestion ;
  - blobs écrits, absences persistées ;
  - taux de succès des caches ;
  - rejets de rate limit ;
  - réponses `426` de la politique client.

  Elles sont exposées sur un **`/metrics` Prometheus, sur un port interne** jamais
  routé par nginx, et scrapées par la stack de l'hôte.
- **Traces** : propagation W3C `traceparent` de bout en bout. L'export OTLP s'active
  quand l'infra (`infra-vps`) expose un backend de traces ; d'ici là, les traces
  servent déjà la corrélation des logs.
- **`build_info`** avec `APP_REVISION`, et label OCI `org.opencontainers.image.revision`
  sur chaque image : une régression se relie à un déploiement.
- Santé : `/healthz` (vivant) et `/readyz` (Postgres joignable, stockage inscriptible),
  en **HTTP**. Fini la sonde `cgi-fcgi`. `start_period` et limites mémoire conservés
  dans `compose.deploy.yaml` (règle : 2 × le pic observé).
- Le panneau « monitoring » de l'admin lit santé et métriques depuis l'API ; il ne
  refait pas Grafana.

### Tests

| Niveau | Outil | Portée |
|---|---|---|
| Unitaire back | xUnit v3 | domaine pur : éditions, règles de builds, résolution d'URL, **chaque particularité Data Dragon** de [`heritage.md`](../heritage.md) § 5 |
| Intégration back | `WebApplicationFactory` + **Testcontainers Postgres** | vraie base, fixtures Data Dragon **enregistrées** (rejouées par un `HttpMessageHandler`) : plus aucun test sauté |
| Contrat | xUnit | `/v1` : statuts, en-têtes `X-RateLimit-*`, enveloppe d'erreur, figés sur des réponses de référence capturées depuis `go-api` avant la bascule |
| Parité | script + xUnit | datasets et manifestes normalisés, PHP vs .NET, sur un échantillon de versions et de langues (lot 1) |
| Unitaire front | Vitest | services, fonctions pures, pipes |
| E2E | Playwright | stack Compose complète, avec assertions SEO (canonique, hreflang, JSON-LD) et accessibilité |
| Mises à jour | pipelines de release | desktop N-1 → N ; Android : retour arrière d'un bundle défectueux |

Règle inchangée : chaque feature arrive avec ses tests (comportement nominal + cas
limites introduits), dans le même commit.

### CI / CD

- **Build once, promote by retag** conservé. Images : `lodb-api`, `lodb-web-ssr`,
  `lodb-nginx`.
- Jobs :
  - build et tests .NET (avec Testcontainers) ;
  - lint, **typecheck**, tests et **build** Angular (deux manques actuels) ;
  - contrôle de dérive du client OpenAPI ;
  - E2E Playwright ;
  - build des images.
- **Migrations EF Core avant la bascule du trafic** :
  - elles tournent dans un conteneur éphémère (`efbundle`) ;
  - elles suivent le modèle expand/contract (on ajoute d'abord, on retire plus tard) ;
  - on ne sert donc plus jamais du code neuf sur un schéma ancien, fenêtre qui existe
    aujourd'hui.
- Releases desktop (matrice Windows / macOS / Linux) et Android (AAB + bundle live
  update) : déclenchées **après** le déploiement de prod, sur le SHA exact (ADR 0008).

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| Serilog | Valable, mais le formateur JSON natif + OpenTelemetry couvrent le besoin sans dépendance |
| Exposer `/metrics` publiquement | Fuite d'informations d'exploitation ; port interne suffisant |
| Mocks de base de données | Ont masqué des écarts de schéma par le passé ; Testcontainers donne la vraie base |

## Conséquences

- **+** Corrélation, métriques applicatives et révision de déploiement : les trois trous
  ouverts de l'audit d'observabilité sont comblés dès le lot 0.
- **+** Plus de tests sautés ; typecheck et build front vérifiés en CI.
- **−** CI plus longue (conteneurs Postgres, E2E) : parallélisation des jobs et cache des
  dépendances à soigner dès le départ.
