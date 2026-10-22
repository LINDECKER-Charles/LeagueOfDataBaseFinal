# Architecture de LeagueOfDataBase

Vue d'ensemble du système tel qu'il tourne aujourd'hui : services, chemin d'une requête, flux
principaux, environnements et dépôt. Les invariants font foi dans [`AGENTS.md`](../../AGENTS.md) ;
chaque décision dans son ADR ([`docs/reecriture/adr/`](../reecriture/adr/), l'amendement daté le
plus récent l'emporte). Comportements voulus : [`pieges.md`](pieges.md). Réglages et secrets :
[`configuration.md`](../guides/configuration.md). Commandes et ports :
[`developpement.md`](../guides/developpement.md).

## Services

Un seul hôte applicatif, `LoDb.Api`, sert `/api`, `/v1` et `/webhooks` et porte l'ingestion et
toutes les tâches de fond ([ADR 0002](../reecriture/adr/0002-hote-serveur-unique.md)). Chaque
service est durci (`x-hardening` de `compose.yaml` : racine en lecture seule, aucune capacité,
sauf les quelques-unes que l'entrypoint de `postgres` reprend par `cap_add`) et appelle ses voisins par leur alias préfixé (`lodb-api`, `lodb-postgres`…).

| Service | Image | Rôle |
|---|---|---|
| `api` | `docker/api/` | `LoDb.Api` sur 8080, métriques sur 9464 ; seul écrivain du volume `ddragon` |
| `migrate` | même image, `migrate` | one-shot : migrations EF, rôles `lodb_app` et `lodb_sauvegarde` ; l'API attend sa réussite |
| `states` | même image, `catalog states build` | one-shot après `migrate` : rattrape l'index des états d'entité (`ddragon` en lecture seule, rôle `lodb_app`) ; l'API attend sa réussite |
| `web-ssr` | `docker/web-ssr/` | Angular SSR (Node) des visiteurs sur 4000 ; écrit les bundles dans `front-builds` |
| `web-ssr-prerender` | même image | SSR de la passe de précalcul, joint par l'API seule, jamais par nginx ([ADR 0011](../reecriture/adr/0011-catalogue-pre-rendu-et-cdn.md)) |
| `nginx` | `docker/nginx/` | frontal sur 8080 : caches de pages, `/cdn/blobs/`, lecture du stockage objet |
| `postgres` | `postgres:17-alpine` | sur le réseau `data` seul, partagé avec `api`, `migrate` et `states` |
| `mailpit` | dev seulement | puits SMTP des e-mails de compte (`compose.override.yaml`) |
| `pages-store` | dev seulement | stockage objet simulé par moto, en mémoire (`compose.override.yaml`) |

Fichiers Compose et environnements qui les appliquent (`.github/workflows/_deploy.yml` construit
`COMPOSE_FILE` ; jamais `compose.override.yaml` sur un hôte) :

| Fichier | Local | Staging | Prod | Apport |
|---|---|---|---|---|
| `compose.yaml` | oui | oui | oui | services, volumes, réseaux `default` et `data`, défauts locaux |
| `compose.override.yaml` | oui | — | — | ports publiés, `Development`, limites relevées pour l'E2E, `mailpit`, `pages-store` |
| `compose.deploy.yaml` | — | oui | oui | images du registre, secrets en fichiers, quotas CPU/mémoire, réseaux `edge` et `observability`, `data` interne, labels de l'edge |
| `compose.staging.yaml` | — | oui | — | petite part de l'hôte partagé avec la prod (~0,75 cœur), passe ralentie |
| `compose.staging-auth.yaml` | — | si `LODB_STAGING_BASIC_AUTH_*` | — | authentification basique à l'edge sur les pages |
| `compose.umami.yaml` | — | — | oui | déclaration du site à l'Umami de l'hôte, tableau `stats.`, relevé d'audience |
| `compose.sauvegarde.yaml` | — | — | oui | Postgres rejoint le réseau `sauvegardes` d'`infra-vps` |

## Chemin d'une requête

```
visiteur, app
   │ HTTPS
   ▼
Caddy, edge partagé (infra-vps) ── /u/* (prod) ──► Umami (infra-vps)
   │ réseau `edge`, en-tête X-LoDb-Edge (sinon 403)
   ▼
nginx :8080 ── www., .fr ──► 301 vers l'hôte canonique
   │
   ├─ api.*  /v1/ /healthz /readyz ───────────────────────────────► api :8080
   ├─ /api/  /v1/  /webhooks/ ────────────────────────────────────► api :8080
   ├─ /cdn/blobs/{sha256}.{ext} ──► volume ddragon (lecture seule)
   ├─ /build/ ──► lodb_assets ──► web-ssr ── 404 ──► stockage build/
   ├─ /{locale}/{version}/… (ancien patch, sans query)
   │      ──► lodb_archive ──► stockage pages/{epoch}/… ── raté ──► web-ssr (épinglée)
   └─ autres pages ──► lodb_pages ──► web-ssr :4000 ──► api ──► postgres
```

- Le site (`sites/site.conf`) et le sous-domaine `api.` (`sites/api.conf`, `server_name
  ~^api\.`) vivent dans le même conteneur ; Caddy les distingue par `Host` (labels `caddy_0`,
  `caddy_1` de `compose.deploy.yaml`). Le sous-domaine ne sert que `/v1/`, `/healthz`, `/readyz`.
- Le dernier patch épinglé dans le chemin ne passe jamais par le stockage : nginx le lit dans
  `/api/meta` toutes les 30 s (`njs/latest-version.js`).
- Tout raté du stockage (404, 403, 429, 5xx, délai) retombe sans bruit sur le SSR ;
  `X-LoDb-Source: cache|store|ssr` dit d'où vient une page épinglée.
- Le SSR ne lit aucun cookie et ne détient aucun secret ; la locale est en préfixe d'URL, l'id
  fait foi, le slug est décoratif ([ADR 0005](../reecriture/adr/0005-web-ssr-urls-et-seo.md)).
- Pendant le remplacement des conteneurs, toute page reçoit la page de maintenance en 503
  (`server.d/maintenance.conf`) ; l'API, `/v1/` et les blobs gardent leur réponse.
- **Cloudflare devant le site est prévu, pas construit** : section « CDN Cloudflare » de la
  [spécification](../produit/pre-rendu-catalogue-cdn.md) et § « CDN » de l'ADR 0011.

## Flux principaux

**Ingestion Data Dragon et CommunityDragon**
([ADR 0003](../reecriture/adr/0003-ingestion-proactive-et-taches-de-fond.md),
[ADR 0004](../reecriture/adr/0004-stockage-etat-et-scaling.md)). `PatchWatchJob` découvre les
versions, `VersionIngestionWorker` en ingère une à la fois sous verrou (datasets, puis images),
`OnDemandIngestionWorker` sert la longue traîne dans des budgets bornés. Toute sortie passe par
le client nommé `ddragon` (`src/LoDb.Ingestion/Egress/`), limité à `ddragon.leagueoflegends.com`
et `raw.communitydragon.org`. Sur le volume `ddragon` (`StorageLayout.cs`) : datasets
`data/{version}/{lang}/{type}.json` et blobs `blobs/{sha256}.{ext}`, écrits de façon atomique ;
nginx ne sert que `/cdn/blobs/`. Le manifeste (`ddragon_version`, `ddragon_asset`) est en
Postgres.

**Promotion d'un patch.** Une version complète devient la dernière si aucune plus récente n'est
promue (`Pipeline/Versions/VersionStates.cs`). Les `IPromotionListener` réagissent dans l'ordre
de l'étape que chacun déclare (`PromotionStage`, tri stable de `VersionIngestion`, quel que soit
l'enregistrement) :

1. `StateIndex` : l'index des états d'entité s'étend jusqu'à la version promue
   (`StateIndexPromotionListener`, `LoDb.Ingestion/Catalog/States/`) ;
2. `Prerender` : une fois l'index à jour, l'unité du patch remplacé (seulement si
   `LoDb__Prerender__AutoEnqueueOnPromotion`), puis la file des pages des états que la promotion
   a clos (`PrerenderPromotionListener`) ;
3. `Announce` : IndexNow (`Modules/Seo/IndexNow/`) : les URL courtes, les pages de patch qui ont
   changé quelque chose et les ouvreurs des états clos depuis le patch promu avant.

Les étapes 2 et 3 lisent dans l'index ce que la promotion a changé ; un écouteur qui échoue est
journalisé sans défaire la promotion, et les réconciliations (`catalog.states.reconcile`,
`prerender.reconcile`) rattrapent l'index puis le précalcul. `LatestCatalogWarmer` épingle en
mémoire les catalogues de la nouvelle version.

**Index des états d'entité** ([ADR 0005](../reecriture/adr/0005-web-ssr-urls-et-seo.md) et
[ADR 0003](../reecriture/adr/0003-ingestion-proactive-et-taches-de-fond.md), amendements du
2026-10-22). Pour chaque champion, objet, arbre de runes et sort listé, la suite des versions
où sa fiche ne change pas (empreinte SHA-256 de ce qu'elle affiche, catalogue en_US,
`LoDb.Domain/States/`). Tables `catalog_state_generation`, `catalog_state_version` et
`catalog_entity_state`, écrites sous le verrou `catalog:states` par la commande
`catalog states build` (service `states`), la promotion et la réconciliation ; chaque processus
en tient une copie en mémoire (`IEntityStateIndex`) qui se recharge d'elle-même. L'API en tire
la canonique des fiches (`StateView`), leur état, leur historique et leurs changements, les
pages de patch et les sitemaps des anciens patchs.

**Précalcul des anciens patchs** ([ADR 0011](../reecriture/adr/0011-catalogue-pre-rendu-et-cdn.md),
[spécification](../produit/pre-rendu-catalogue-cdn.md)). `PrerenderWorker` parcourt une passe par
(version, langue) et appelle `web-ssr-prerender` en direct (`X-LoDb-Prerender: 1`, que nginx
efface côté public), copie le jeu de bundles du build, puis écrit chaque page en gzip dans le
stockage objet Scaleway de l'environnement (`src/LoDb.Infrastructure/PageStore/`, clés
`pages/{epoch}/…` et `build/…`). nginx lit ce stockage anonymement depuis l'adresse de l'hôte,
derrière `LODB_PAGES_STORE_ENABLED`, dans l'epoch `LODB_ARCHIVE_SERVE_EPOCH`. Le rythme suit le
CPU libre de l'hôte (`PrerenderPaceWorker`) ou une cible manuelle. La file de la demande :
`web-ssr` compte les pages d'anciens patchs qu'il refuse en 503 (`GET /refused` sur 9464),
`PrerenderDemandCollectWorker` les verse dans `prerender_demand`, `PrerenderDemandWorker` les
précalcule, la plus demandée d'abord. État en Postgres (`prerender_pass`, `prerender_unit`,
`prerender_build`), pupitre `/api/admin/prerender`, CLI `prerender unit|pause|resume`.

**Mesure d'audience.** Interne : nginx copie chaque requête de page
(`snippets/analytics-mirror.conf`) vers `/internal/analytics/page`, le routeur du front poste
`/api/analytics/view` pour les navigations internes ; une file en mémoire est écrite par lots
dans `analytics_event` (`AnalyticsWriterWorker`), agrégée dans `analytics_daily`
(`AnalyticsRollupJob`) et purgée après 13 mois (`AnalyticsRetentionJob`).
Umami, en prod seulement : le front poste `/u/api/send` depuis l'hôte canonique, jamais
`/u/script.js` ; `AudienceTrendsWorker` relève chaque heure les vues par page pour `/v1/trends`
([ADR 0012](../reecriture/adr/0012-audience-umami.md)). Détail : [`analytics.md`](analytics.md).

**API publique `/v1` et facturation** ([ADR 0002](../reecriture/adr/0002-hote-serveur-unique.md),
détail : [`api-publique.md`](api-publique.md)).
Clé `lodb_` + 40 hex en `Authorization: Bearer` ou `X-Api-Key`, hash seul stocké ; limites par
clé, métrage par lots, erreurs `{error:{code,message}}` ; contrat
`src/LoDb.Api/openapi/LoDb.Api_public-v1.json`. Stripe : paiement, abonnements, dons et crédits
(`Modules/Billing/`), événements reçus sur `POST /webhooks/stripe`.

**Comptes et identité** ([ADR 0009](../reecriture/adr/0009-identite-authentification-sessions.md)).
ASP.NET Core Identity, hash argon2id, Google OAuth, TOTP. Web : cookie `__Host-` avec garde XSRF
et contrôle d'`Origin` ; apps : jetons bearer et refresh par familles révocables. Aucune session
serveur ; clés Data Protection en Postgres, chiffrées par le certificat `data-protection.pfx`.
E-mails par une outbox en Postgres (`EmailOutboxWorker`, SMTP).

**Apps** ([ADR 0007](../reecriture/adr/0007-coquilles-desktop-et-android.md),
[ADR 0008](../reecriture/adr/0008-mises-a-jour-integrees.md)). Desktop : `src/LoDb.Desktop`,
Photino derrière `IDesktopShell`, proxy loopback qui détient les jetons, mises à jour Velopack.
Android : Capacitor 8 (`src/LoDb.Web/android`), live update signé
(`@capawesome/capacitor-live-update`) publié par `/api/admin/client-policy`. Chaque requête d'app
porte `X-LoDb-Client: {plateforme}/{version}` ; l'API répond `426` à une version refusée.
Releases : `release-desktop.yml`, `release-android.yml`
([`release-desktop.md`](../guides/release-desktop.md),
[`release-android.md`](../guides/release-android.md)).

**Observabilité** ([ADR 0010](../reecriture/adr/0010-observabilite-tests-ci.md)). Logs JSON
d'une ligne sur stdout (`Hosting/Logging/JsonLineConsoleFormatter.cs`, pino côté SSR), driver
`json-file`, collectés par `infra-vps`. nginx ouvre un `traceparent` par requête relayée.
`/metrics` sur 9464 pour `api`, `web-ssr` et `web-ssr-prerender`, lu par la collecte sur le
réseau `observability`, jamais publié ni routé. `/healthz` ne dépend de rien, `/readyz` vérifie
Postgres et le stockage. Contrat des métriques : [`metriques.md`](../guides/metriques.md).

**Sauvegardes.** Avant chaque `migrate`, le job de déploiement prend un `pg_dump` relu, dix
gardés dans `.deploy/backups/` (retour arrière). En prod, `infra-vps` sauvegarde la base hors
de l'hôte chaque nuit par le réseau `sauvegardes` et le rôle en lecture `lodb_sauvegarde`,
journal dans `backup_run` ([`sauvegardes.md`](../guides/sauvegardes.md)).

**SEO.** Sitemaps, `robots.txt` et `llms.txt` viennent de l'API (`server.d/seo.conf`), les
anciennes URL du site passent par `/api/legacy` (`server.d/legacy-redirects.conf`). Le code
applique la règle des canoniques du 2026-10-14 (une page indexée par entité, ADR 0005) ; une
page indexée par état a été décidée le 2026-10-22
([`seo-une-page-par-etat.md`](../produit/seo-une-page-par-etat.md)), pas encore implémentée.
Doc vivante : [`seo-indexabilite.md`](../produit/seo-indexabilite.md).

## Environnements et livraison

| Env | Projet Compose | Hôtes | Particularités |
|---|---|---|---|
| local | `lodb-dev`, emplacements `lodb-dev-e1`, `-e2` | `localhost:18080` (181xx, 182xx) | aucun secret, stockage simulé |
| staging | `lodb-staging`, `:staging` | `test.league-of-data-base.com`, `api.test.` | `LODB_NOINDEX=1`, même hôte que la prod |
| prod | `lodb-prod`, `:prod` `:latest` | `league-of-data-base.com` (`.fr` en 301), `api.` | Umami, sauvegardes hors hôte |

Chemin d'un changement (`.github/workflows/ci.yml`) :

1. Push sur `dev` : contrôles en parallèle (.NET, front, contrat, workflows, chaîne
   d'approvisionnement, E2E ; i18n informatif).
2. Fusion automatique `dev` → `test`, build des trois images `api`, `web-ssr`, `nginx`
   (`_build.yml`, scan Trivy, tag `:staging`), déploiement du staging (`_deploy.yml`).
3. Staging sain : ses digests reçoivent le tag `:staging-validated`.
4. Fusion manuelle `test` → `main` : `_promote.yml` pose `:prod` et `:latest` sur les digests
   `:staging-validated`, sans rebuild, et refuse si `:staging` les a quittés ; puis
   déploiement de la prod.

Un déploiement : `.env` découpé (secrets en fichiers sous `.deploy/secrets/`), pull, `pg_dump`,
`migrate`, `states` (index des états), page de maintenance, `up --wait` des services des visiteurs puis du SSR de la passe,
tests de fumée. Détail : [`github-actions-secrets.md`](../guides/github-actions-secrets.md).

## Dépôt

```
compose*.yaml  .env.staging.example  .env.prod.example
docker/{api,web-ssr,nginx,android-build}/   images ; nginx : sites/ snippets/ server.d/ njs/
src/LoDb.Domain/          pur : Versions/ Languages/ Editions/ Paths/ Catalog/ Builds/ Derived/
                          Text/
src/LoDb.Ingestion/       Egress/ Ddragon/ Pipeline/ Queue/ Catalog/ Images/ Normalization/
src/LoDb.Infrastructure/  Persistence/ Storage/ PageStore/ Jobs/ Locks/ Outbox/ Audit/
                          Analytics/ Audience/ DataProtection/
src/LoDb.Api/             Program.cs Hosting/ Cli/<Zone>/ Workers/<Zone>/ Modules/<M>/ openapi/
src/LoDb.Web/             Angular : src/app/{core,ui,features}  src/server/  public/i18n/  android/
src/LoDb.Desktop/         coquille Photino : Shell/ Proxy/ Auth/ Updates/ …
tests/LoDb.{Domain,Ingestion,Infrastructure,Api,Desktop}.Tests/  LoDb.Testing/  LoDb.E2E/
tests/LoDb.Parity/  tests/fixtures/
tools/                    ci, deploy, compose, nginx, prerender, routing, umami, i18n, contract…
```

- Références : `Api` → `Ingestion` → `Infrastructure` → `Domain` (`ProjectReference` des
  `.csproj`) ; découpage en projets :
  [ADR 0001](../reecriture/adr/0001-reecriture-dotnet-angular.md).
- `Infrastructure` : `PageStore/` (client S3 signé du stockage des pages), `Audience/` (relevé
  Umami), `Locks/` (verrou consultatif Postgres), `Jobs/` (`PeriodicJob`), `Outbox/` (e-mails).
- `Api` : modules `Accounts`, `Admin`, `Analytics`, `Audit`, `Billing`, `Builds`, `Catalog`,
  `ClientPolicy`, `Contact`, `Legacy`, `Profiles`, `PublicApi`, `Seo`, `Trends` ; tâches sous
  `Workers/<Zone>/`, sous-commandes sous `Cli/<Zone>/`, découvertes par convention.
- `src/LoDb.Web/src/server/` : serveur SSR (admission des rendus, compression, CSP, métriques,
  file des refus) ; le front n'utilise que le client généré depuis OpenAPI
  ([ADR 0006](../reecriture/adr/0006-front-angular-multi-cibles.md)).
