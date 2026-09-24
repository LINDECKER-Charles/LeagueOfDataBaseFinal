# Lot 0 — Socle

Objectif du lot : une solution .NET et un workspace Angular qui se construisent, se testent,
tournent en conteneurs et passent en CI, avec l'observabilité en place dès le départ et
tous les points d'accroche dont les lots suivants ont besoin pour travailler en parallèle.
Rien de fonctionnel encore : une page SSR « hello » localisée suffit.

Critère de sortie local : suites vertes ; `/en/` rendu en SSR par la stack `lodb-next` ;
`/healthz`, `/readyz` et la métrique `lodb_build_info` lisibles ; logs JSON d'une ligne
par enregistrement.

## L0.1 — Solution .NET et hôte `LoDb.Api`

- **Objectif** : une solution qui compile sans avertissement, quatre projets serveur, les
  projets de test, et un hôte qui démarre, journalise en JSON, expose santé, métriques et
  documents OpenAPI, avec **tous les points d'enregistrement** du plan pré-créés.
- **À lire** : ADR [0001](../adr/0001-reecriture-dotnet-angular.md),
  [0002](../adr/0002-hote-serveur-unique.md), [0010](../adr/0010-observabilite-tests-ci.md) ;
  [`logging.md`](../../guides/logging.md) et [`observabilite.md`](../../guides/observabilite.md) ;
  en référence de structure, `src/App.GitHealth.*` du dépôt GitHealth.
- **Périmètre** : `LoDb.slnx`, `global.json`, `Directory.Build.props`,
  `Directory.Packages.props`, `.editorconfig`, ajouts à `.gitignore` (sorties .NET et
  Angular), `src/LoDb.Domain/`, `src/LoDb.Ingestion/`, `src/LoDb.Infrastructure/`,
  `src/LoDb.Api/`, `tests/Directory.Build.props`, `tests/LoDb.*.Tests/` (squelettes),
  `tests/LoDb.Testing/` (bibliothèque de test partagée).
- **Conception** :
  - Références : `Api` → `Ingestion` → `Infrastructure` → `Domain`. `Domain` ne référence
    aucun paquet d'I/O.
  - `Program.cs` : `WebApplication.CreateBuilder`, puis :
    - `Add<Module>`/`Map<Module>` des 14 modules du plan (§3), chacun dans un fichier
      `Modules/<Module>/<Module>Module.cs` aux méthodes vides ;
    - `AddLoDb<Zone>` des zones techniques du §5.1 (`Egress`, `Storage`, `Persistence`,
      `Jobs`, `Ddragon`, `Ingestion`, `Catalog`, `Outbox`, `Audit`, `DataProtection`,
      `Analytics`), chacune dans un fichier vide à l'emplacement de sa zone (par exemple
      `src/LoDb.Infrastructure/Storage/StorageRegistration.cs`), que le chantier de la zone
      remplira ;
    - Kestrel sur 8080 et **9464** pour les métriques.
  - Aucun `Add*` ni `Map*` ne fait d'I/O ni ne lève d'exception : la génération OpenAPI
    exécute `Program` sans démarrer l'hôte.
  - Logs : formateur console JSON sur une ligne, horodatage UTC, portées activées pour
    porter `trace_id`/`span_id` ; les `EventName` suivent `domaine.sujet.resultat`.
  - OpenTelemetry : métriques ASP.NET Core, `HttpClient`, runtime, compteurs `LoDb.*` ;
    traces avec propagation W3C ; exporteur Prometheus sur `*:9464/metrics` ; OTLP activé
    seulement si `OTEL_EXPORTER_OTLP_ENDPOINT` est défini.
  - Jauge `lodb_build_info{revision,version}` (valeur 1) depuis `APP_REVISION` et la version
    de l'assembly.
  - Santé : `/healthz` sans dépendance ; `/readyz` vérifie Postgres (`SELECT 1` via
    `NpgsqlDataSource`, chaîne `ConnectionStrings:LoDb`) et l'écriture dans la racine du
    stockage (`LoDb:Storage:Root`).
  - OpenAPI : documents `app` (groupes `/api`) et `public-v1` (groupes `/v1`), générés dans
    `src/LoDb.Api/openapi/` **seulement** quand la propriété MSBuild dédiée est activée
    (utilisée par le script `api:generate` et par la CI), jamais à chaque build ;
    `MapOpenApi()` en Development seulement.
  - `AddProblemDetails()` et gestionnaire d'exceptions pour `/api` ; `ForwardedHeaders`
    limité au réseau de nginx.
  - Sous-commandes (`src/LoDb.Api/Cli/<Zone>/`) : interface `ICliCommand` (nom, exécution),
    implémentations **découvertes par convention** (réflexion sur l'assembly `LoDb.Api`),
    pour qu'aucun chantier n'ait de fichier commun à modifier. Le choix se fait **avant** la
    construction de l'hôte web : `healthcheck` (GET `http://127.0.0.1:8080/healthz`, code de
    sortie 0 ou 1) s'exécute sans DI ni port ; les autres commandes tournent dans un hôte
    générique sans Kestrel, avec les mêmes enregistrements `Add*`.
  - Tâches de fond : toute sous-classe de `BackgroundService` sous
    `src/LoDb.Api/Workers/<Zone>/` est enregistrée par la même convention ;
    `LoDb:Workers:Enabled=false` les coupe toutes (tests d'intégration, génération OpenAPI).
  - `tests/LoDb.Testing/` : `PostgresContainerFixture` (Testcontainers, `postgres:17-alpine`),
    `ApiFactory` (`WebApplicationFactory<Program>`), `FakeTimeProvider`.
  - Paquets de test : xUnit v3, `Microsoft.AspNetCore.Mvc.Testing`,
    `Testcontainers.PostgreSql`.
- **Tests** : `/healthz` 200 ; `/readyz` 503 sans Postgres, 200 avec le conteneur ;
  `/metrics` servi sur 9464 et absent sur 8080 ; une ligne de log est un JSON valide d'une
  seule ligne avec son `EventName` ; génération des deux documents OpenAPI à la demande ;
  `healthcheck` n'ouvre aucun port et rend les bons codes de sortie ; une commande et une
  tâche de fond factices sont découvertes par la convention, et la tâche ne démarre pas
  quand `LoDb:Workers:Enabled` vaut `false`.
- **Acceptation** : `dotnet build LoDb.slnx -c Release` sans avertissement,
  `dotnet test` vert, `dotnet run --project src/LoDb.Api` répond sur `/healthz`.
- **Taille** : L.

## L0.2 — Workspace Angular

- **Objectif** : un workspace Angular 22 dans `src/LoDb.Web` qui produit les builds `web`
  (SSR) et `shell` (statique), avec le serveur SSR, Transloco, la couche plateforme, le
  gabarit racine et ses emplacements, les règles de lint d'architecture, Vitest, et une page
  d'accueil « hello » localisée.
- **À lire** : ADR [0005](../adr/0005-web-ssr-urls-et-seo.md),
  [0006](../adr/0006-front-angular-multi-cibles.md) ; `app/package.json` (dépendances
  actuelles) ; le front Angular du dépôt Bloodborne Legendary Run (référence Angular +
  Capacitor).
- **Périmètre** : `src/LoDb.Web/**`, sauf `src/app/features/**` (créés plus tard) et
  `android/`.
- **Conception** :
  - Angular 22, `@angular/ssr`, zoneless, constructeur `application`, Vitest comme
    lanceur de tests.
  - `angular.json` : configuration `web` (`outputMode: server`, entrée `src/server.ts`),
    `shell` (`outputMode: static`, sans SSR ni prérendu, environnement `shell` qui porte
    l'origine publique de l'API pour Android), `development`. Option de sécurité `autoCsp`
    activée pour `web`.
  - Scripts npm, tous déclarés ici pour que personne ne se dispute `package.json` plus
    tard : `start`, `build:web`, `build:shell`, `serve:ssr`, `lint`, `typecheck` (`tsc
    --noEmit` sur les tsconfig app et spec), `test` (`ng test --watch=false`), et quatre
    scripts qui appellent des fichiers encore vides, remplis ensuite : `api:generate`,
    `api:check` (L2.2), `i18n:convert`, `i18n:report` (L3.3). Un script vide annonce qu'il
    n'est pas encore disponible et sort en 0.
  - `src/server.ts` : `AngularNodeAppEngine`, port 4000, `/healthz`, fichiers statiques à
    cache long (noms hachés), `allowedHosts` depuis `LODB_ALLOWED_HOSTS`, logs JSON pino
    (`http.request.served` avec statut, durée, `trace_id` repris de `traceparent`). Le
    serveur ne lit aucun cookie et ne détient aucun secret.
  - `app.config.ts` : routeur, `provideHttpClient(withFetch(), withInterceptors(...))`,
    `provideClientHydration(withIncrementalHydration(), withHttpTransferCacheOptions(...))`,
    jeton `API_BASE_URL` (origine sans chemin, §5.2) avec `HTTP_TRANSFER_CACHE_ORIGIN_MAP`
    côté serveur, Transloco ; `app.config.server.ts` pour le SSR.
  - Routage provisoire : `path: ':locale'` avec une garde `canMatch` sur les 21 locales
    (liste provisoire dans `core/i18n/`, remplacée par le tableau généré en L2.2) ; `/`
    redirigé vers `/en/` (la redirection selon `Accept-Language` revient à L3.1).
  - Transloco : chargeur **HttpClient** (URL relative des catalogues), suivi par le SSR et
    repris par le cache de transfert ; catalogues `en` et `fr` minimaux (remplacés par la
    conversion de L3.3).
  - Gabarit racine : `<lodb-shell>` (`core/layout/shell`, version minimale) qui projette
    `[lodbSlot=switcher]`, `[lodbSlot=account]`, `[lodbSlot=banner]`,
    `[lodbSlot=contact]` et le `router-outlet` (contrat du §5.2).
  - `core/platform/` : jeton `PLATFORM`, interface `PlatformService` (§5.2 du plan),
    implémentation `web`, détection au démarrage (`provideAppInitializer`).
  - `index.html` : script inline qui pose `data-theme` depuis le cookie `lod_theme`
    (thèmes `hextech`, `zaun`, `noxus`, `spirit-blossom`), avant tout rendu.
  - ESLint avec les deux règles d'architecture du plan (§4).
- **Tests** : garde de locale (21 acceptées, le reste refusé) ; détection de plateforme ;
  lecture du thème par le script inline (fonction pure extraite).
- **Acceptation** : `npm ci`, `lint`, `typecheck`, `test`, `build:web`, `build:shell` verts ;
  `node dist/web/server/server.mjs` sert `/en/` avec `<html lang="en">` rendu côté serveur.
- **Taille** : L.

## L0.3 — Conteneurs et Compose `lodb-next`

- **Objectif** : trois images et une stack Compose `lodb-next` (api, web-ssr, nginx,
  postgres, Mailpit en dev) qui tourne en local sur les ports du plan (§6.3), avec la
  disposition nginx dont les lots suivants ont besoin.
- **À lire** : `compose.yaml`, `compose.override.yaml`, `compose.deploy.yaml`,
  `docker/nginx/` (`default.conf`, `snippets/security-headers.conf`),
  [`migration-edge-proxy.md`](../../guides/migration-edge-proxy.md),
  [`observabilite.md`](../../guides/observabilite.md) (driver `json-file` imposé).
- **Périmètre** : `docker/next/**`, `compose.next.yaml`, `compose.next.override.yaml`,
  `compose.next.deploy.yaml` (première version), `.env.next.example`.
- **Conception** :
  - `api` : build multi-étapes sur `mcr.microsoft.com/dotnet/sdk:10.0`, exécution sur
    `aspnet:10.0` *chiseled* avec ICU (`noble-chiseled-extra`), utilisateur non root,
    ports 8080 et 9464, sonde `dotnet LoDb.Api.dll healthcheck` en forme exec.
  - `web-ssr` : build `npm ci` + `build:web` sur `node:24`, exécution `node:24-slim`
    (utilisateur `node`), port 4000, sonde `node -e` sur `/healthz`. Contexte de build à la
    racine du dépôt (le build copiera aussi `app/public/changelog/`, cf. L3.10), avec un
    `.dockerignore` propre au Dockerfile qui n'admet que le nécessaire.
  - nginx (`nginx:1.31-alpine`, gabarits `envsubst` de l'image officielle) :
    - `sites/site.conf` (le site) et `sites/api.conf` (sous-domaine `api.`, qui ne sert que
      `/v1/` et `/healthz`, le reste en 404, comme go-api aujourd'hui) ;
    - dans le site : `/cdn/blobs/` en alias sur le volume `storage` monté en lecture seule,
      `public, immutable`, un an ; tout autre `/cdn/` en 404 ; `/api/`, `/v1/`, `/webhooks/`
      vers `api:8080` ; tout le reste vers `web-ssr:4000` ; `/healthz` local ;
    - `include /etc/nginx/server.d/*.conf;` **dans** le `server` du site (fichiers réservés
      aux lots suivants, §7.3) ; dossier `snippets/` pour ce qui s'inclut dans une
      `location` ;
    - IP réelle : `set_real_ip_from` sur `LODB_EDGE_CIDR` (réseau de l'edge),
      `real_ip_header X-Forwarded-For`, puis `X-Forwarded-For` transmis à l'API et au SSR ;
    - en-têtes constants (HSTS `max-age=63072000; includeSubDomains; preload`, `nosniff`,
      `Referrer-Policy`, CORP) ; `X-Robots-Tag: noindex, nofollow` quand `LODB_NOINDEX=1` ;
    - hygiène d'hôte à l'edge : `www.` et `.fr` en 301 vers l'hôte canonique
      (`LODB_CANONICAL_HOST`) ;
    - zone `proxy_cache` déclarée (utilisée par L3.11).
  - Tous les services en `json-file` (10m × 3), sondes HTTP, `depends_on` sur l'état
    `healthy`. `api` dépendra du service `migrate` ajouté par L1.4.
  - `compose.next.override.yaml` : ports hôte lus dans les variables du §6.3 (valeurs par
    défaut de la stack d'intégration), `ASPNETCORE_ENVIRONMENT=Development`, Mailpit.
  - `compose.next.deploy.yaml` : images `ghcr.io/<owner>/lodb/{api,web-ssr,nginx}:${IMAGE_TAG}`,
    réseau externe `edge`, labels Caddy sur nginx pour `CADDY_DOMAINS` et
    `API_CADDY_DOMAINS`, limites mémoire provisoires, aucun port hôte, Postgres propre à
    `next`.
- **Tests** : `docker compose config` valide pour dev et déploiement ; stack démarrée avec
  toutes les sondes `healthy` ; via nginx : `/en/` en 200 rendu SSR, `/healthz` 200,
  `/api/inconnu` en ProblemDetails JSON (pas en HTML), `/cdn/blobs/x` 404 ; sous-domaine
  `api.` (en-tête `Host`) : `/v1/…` routé, `/en/` en 404 ; `/metrics` lisible depuis le
  réseau de la stack ; une deuxième stack sur l'emplacement 1 démarre sans conflit ;
  utilisateurs non root vérifiés (`docker inspect`).
- **Acceptation** : les tests ci-dessus, plus la mémoire relevée par `docker stats` (base des
  limites de L8.1).
- **Dépend de** : L0.1, L0.2. **Taille** : M.

## L0.4 — CI, squelette E2E, déploiement de `next`

- **Objectif** : des workflows pour la nouvelle stack, sans toucher aux existants ; le projet
  Playwright ; un workflow de déploiement de `next` prêt à recevoir ses secrets.
- **À lire** : `.github/workflows/{ci,_tests,_build,_promote,_deploy}.yml`,
  [`github-actions-secrets.md`](../../guides/github-actions-secrets.md),
  `.github/dependabot.yml`, ADR [0010](../adr/0010-observabilite-tests-ci.md).
- **Périmètre** : `.github/workflows/next-ci.yml`, `next-build.yml`, `next-deploy.yml` ;
  ajouts à `.github/dependabot.yml` (NuGet racine, npm `src/LoDb.Web` et `tests/LoDb.E2E`,
  Docker `docker/next/*`) ; `tests/LoDb.E2E/**` (configuration et `specs/public/`) ;
  nouvelle section de `docs/guides/github-actions-secrets.md`.
- **Conception** :
  - Déclencheurs : `pull_request` et `push` filtrés sur `src/**`, `tests/**`,
    `docker/next/**`, `compose.next*`, `LoDb.slnx`, `Directory.*`, `global.json`,
    `.github/workflows/next-*`.
  - Jobs de `next-ci.yml`, en parallèle : `dotnet` (SDK via `global.json`, cache NuGet,
    build, tests avec Testcontainers) ; `front` (Node 24, cache npm, `lint`, `typecheck`,
    `test`, `build:web`, `build:shell`) ; `contract` (`npm run api:check`) ; `i18n`
    (`npm run i18n:report`, non bloquant) ; `e2e` (stack `lodb-next` construite, attente
    des sondes, Playwright). Les jobs `contract` et `i18n` appellent les scripts npm
    pré-déclarés : L2.2 et L3.3 n'ont pas à modifier le workflow.
  - `next-build.yml` : images avec labels OCI et `APP_REVISION`, poussées sur GHCR
    (`:<sha>` et `:next`) sur `push` de la branche d'intégration uniquement.
  - `next-deploy.yml` : `workflow_dispatch`, même mécanique SSH que `_deploy.yml`, secrets
    `NEXT_SSH_KEY`, `NEXT_HOST`, `NEXT_PATH`, `NEXT_SSH_USER`, `ENV_NEXT` ; `pull`, puis
    service éphémère `migrate` (dès qu'il existe, L1.4) **avant** `up -d`, puis smoke test.
  - `tests/LoDb.E2E` : `@playwright/test`, `@axe-core/playwright`, `baseURL`
    `http://localhost:18080` (surchargeable pour les emplacements) ; specs rangées par
    feature (`specs/<feature>/`) ; test de fumée dans `specs/public/` : `/en/` en 200,
    `lang="en"`, aucune erreur console.
  - Validation locale des workflows avec `actionlint` (image `rhysd/actionlint`).
- **Tests** : `actionlint` propre ; le test de fumée passe contre la stack locale.
- **Acceptation** : idem, et `git diff` ne montre que des fichiers nouveaux, les ajouts
  Dependabot et la nouvelle section du guide des secrets.
- **Dépend de** : L0.3. **Taille** : M.

## L0.5 — `CLAUDE.md` et guide de dev de la nouvelle stack

- **Objectif** : pendant la transition, `CLAUDE.md` décrit les deux stacks. Les règles de
  `app/` et `go/` restent ; une section « Nouvelle stack (réécriture) » ajoute
  l'arborescence, les invariants tirés des ADR, les conventions du plan (§4), les
  garde-fous (§6.2), la carte des scopes et les pièges rencontrés au lot 0.
- **À lire** : `CLAUDE.md`, ce plan (§3 à §6), les comptes rendus de L0.1 à L0.4.
- **Périmètre** : `CLAUDE.md`, `docs/guides/dev-next.md` (commandes, ports, emplacements,
  dépannage), `docs/README.md` (index).
- **Acceptation** : chaque commande du guide, exécutée telle qu'écrite, réussit.
- **Dépend de** : L0.4. **Taille** : S.
