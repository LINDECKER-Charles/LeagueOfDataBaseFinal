# Plan d'implémentation de la réécriture

> **Statut** : prêt à exécuter — 2026-09-24.
> Ce plan décline le [plan de migration](plan-migration.md) en **chantiers** exécutables :
> chaque lot y est découpé en chantiers à périmètre de fichiers borné, avec ses entrées, ses
> tests et son critère d'acceptation. Il fixe aussi les conventions communes, l'ordre
> d'exécution et une version **vérifiable en local** de chaque critère de sortie.
> Le fond (quoi, pourquoi) reste dans le [README](README.md), les [ADR](adr/),
> [`heritage.md`](heritage.md) et [`plan-migration.md`](plan-migration.md) : en cas de
> désaccord, ce sont eux qui font foi, et ce plan se corrige. Les écarts assumés sont listés
> au §13.

## Comment lire ce plan

| Section | Contenu |
|---|---|
| §1 à §2 | Portée, principes d'exécution |
| §3 à §5 | Arborescence, conventions de code, décisions techniques transverses |
| §6 | Environnement de développement vérifié, commandes, ports |
| §7 | Ordre d'exécution : graphe des lots, table des chantiers, fichiers partagés |
| §8 à §9 | Jalons de test, critères de sortie locaux, définition de terminé |
| §10 | Couverture : chaque point de `heritage.md` et chaque ADR → un chantier |
| §11 à §13 | Opérations hôte et humaines, risques d'exécution, écarts assumés |

Le détail de chaque chantier est dans [`implementation/`](implementation/), un fichier par lot.
Un chantier porte un identifiant stable `Lx.y` (lot x, chantier y) et décrit :

- **Objectif** : ce qui doit exister à la fin ;
- **À lire** : documents et code de l'ancienne stack qui font référence de comportement ;
- **Périmètre** : les fichiers et dossiers que ce chantier est **seul** à écrire ;
- **Conception** : les choix déjà arrêtés, à ne pas rediscuter dans le chantier ;
- **Tests** : ce qui doit être couvert (comportement nominal et cas limites) ;
- **Acceptation** : la vérification qui clôt le chantier ;
- **Dépend de** et **taille** (S, M, L : ordre de grandeur relatif).

## 1. Portée

**Dans le plan** : les lots 0 à 10 du plan de migration, c'est-à-dire tout ce qui se
construit dans le dépôt :

- le code et les tests de `src/` et `tests/` ;
- les conteneurs et la Compose de la nouvelle stack (`docker/next/`, `compose.next*.yaml`) ;
- les workflows de la nouvelle stack (`.github/workflows/next-*.yml`) ;
- les scripts (`tools/next/`), les rapports (`docs/reecriture/rapports/`) et les runbooks ;
- la mise à jour de `CLAUDE.md` au lot 0.

**Hors du plan** :

- **La stack en service est en lecture seule** : `app/`, `go/`, `docker/nginx/`,
  `docker/php/`, `compose.yaml`, `compose.override.yaml`, `compose.deploy.yaml` et les
  workflows existants ne sont pas modifiés. Elle sert de référence de comportement et,
  démarrée en local, de source de références (schéma, parité des données, contrat `/v1`).
- Les **bugs latents** de [`heritage.md`](heritage.md) § 4 se corrigent sur `dev`, hors de
  cette branche. Le plan veille seulement à ne pas les reproduire.
- Tout ce qui exige un compte, un secret, le VPS ou une décision humaine est listé au §11,
  à l'endroit où il bloque. Les critères de sortie formulés « sur `next` » sont vérifiés ici
  sur la stack `lodb-next` **locale** ; le déploiement de `next` lui-même est une opération
  hôte.
- **Aucune fonctionnalité nouvelle** au-delà de ce que les ADR décident : la cible est la
  parité, plus les corrections structurelles actées.

## 2. Principes d'exécution

1. **Réécriture, pas portage.** L'ancien code se lit pour capturer un comportement, une
   règle ou un cas limite ; ses contournements (`heritage.md` § 1 à 3) ne se recopient
   jamais. Les tests PHPUnit (`app/tests/Unit`, 597 tests), Go et Vitest
   (`app/assets/vue/**/*.spec.ts`, environ 257 cas) sont une source de cas de test.
2. **Contrats d'abord.** Types du domaine, schéma de base et document OpenAPI sont posés tôt
   et n'ont qu'un propriétaire à la fois. Le front ne consomme que le client généré.
3. **Périmètres disjoints.** Un chantier n'écrit que dans son périmètre. Les fichiers
   partagés ont un propriétaire désigné (§7.3) ; un besoin hors périmètre se signale dans le
   compte rendu du chantier, il ne se contourne pas.
4. **Tests livrés avec le code**, dans le même commit. Chaque particularité Data Dragon
   (`heritage.md` § 5) et chaque invariant métier (§ 6) devient un test nommé. Chaque
   chantier front livre aussi ses tests E2E dans `tests/LoDb.E2E/specs/<feature>/`.
5. **Une migration EF par lot**, produite par le chantier de schéma du lot (L1.4, L4.1,
   L6.2). Lui seul écrit entités et configurations EF : depuis EF 9, `Migrate()` échoue
   quand le modèle diverge des migrations. Un besoin de colonne se signale, il ne s'improvise
   pas. Toutes les migrations sont **additives** jusqu'à la phase *contract*, qui suit la
   bascule : l'ancienne stack doit continuer de tourner sur le même schéma.
6. **Artefacts générés** (documents OpenAPI, client TypeScript, `package-lock.json`) : un
   chantier ne les committe jamais ; L2.2 puis les agents d'intégration les régénèrent et
   les committent après chaque fusion.
7. **Rien ne casse l'ancienne stack** : ni ses fichiers, ni ses ports, ni ses volumes, ni
   sa CI.
8. **Chaque lot se clôt par un jalon** (§8) : fusion, suites complètes, stack locale,
   critère de sortie. Les échecs donnent des correctifs, puis une nouvelle vérification.

## 3. Arborescence cible

Elle précise celle de l'[ADR 0001](adr/0001-reecriture-dotnet-angular.md).

```
LoDb.slnx                     solution (.NET 10, format slnx)
global.json                   SDK 10.0.4xx, rollForward latestFeature
Directory.Build.props         net10.0, Nullable, TreatWarningsAsErrors, analyseurs
Directory.Packages.props      versions NuGet centralisées
.editorconfig                 règles C# (et TS via le workspace)
compose.next.yaml             stack lodb-next : migrate, api, web-ssr, nginx, postgres
compose.next.override.yaml    dev : ports hôte (variables), Mailpit
compose.next.deploy.yaml      next / prod : images GHCR, labels Caddy, limites mémoire
.env.next.example
docker/next/
  api/Dockerfile              image lodb-api (hôte et sous-commandes, dont migrate)
  web-ssr/Dockerfile          image lodb-web-ssr
  nginx/                      image lodb-nginx : nginx.conf, sites/ (site, sous-domaine
                              api), server.d/*.conf (inclus dans le server du site),
                              snippets/ (inclus dans une location)
src/
  LoDb.Domain/                pur, sans I/O : versions, langues, éditions, catalogue,
                              faits dérivés, chemins canoniques, builds
  LoDb.Ingestion/             Egress/, Ddragon/, Normalization/, Pipeline/, Queue/,
                              Images/, Catalog/ (catalogue en mémoire, images)
  LoDb.Infrastructure/        Persistence/ (EF Core, migrations), Storage/, Locks/,
                              Jobs/, Outbox/, Audit/, Analytics/, DataProtection/
  LoDb.Api/                   Program.cs, Hosting/, Cli/, Workers/,
                              Modules/<Module>/ (endpoints, services, contrats),
                              openapi/ (documents générés à la demande, commités)
  LoDb.Desktop/               hôte Photino + Kestrel loopback + Velopack
  LoDb.Web/                   workspace Angular : src/app/{core,ui,features},
                              src/styles/, public/i18n/, server.ts, android/
tests/
  LoDb.Domain.Tests/  LoDb.Ingestion.Tests/  LoDb.Infrastructure.Tests/
  LoDb.Api.Tests/     (intégration WebApplicationFactory + contrat /v1)
  LoDb.Desktop.Tests/
  LoDb.Testing/       bibliothèque de test partagée (Postgres, rejeu, horloge)
  LoDb.Parity/        outil et tests de parité PHP ↔ .NET (lot 1)
  LoDb.E2E/           Playwright (TypeScript), axe, assertions SEO ; specs/<feature>/
  fixtures/ddragon/   réponses Data Dragon / CommunityDragon enregistrées
  fixtures/schema/    schéma Doctrine de référence (lot 1)
  fixtures/hashes/    hash de mots de passe produits par PHP (lot 4)
  fixtures/v1/        réponses de référence de go-api (lot 6)
tools/next/           scripts : base, conversion i18n, complétude, parité, bascule
docs/reecriture/rapports/   rapports produits par les chantiers
```

Références entre projets : `Api` → `Ingestion` → `Infrastructure` → `Domain`.

Modules prévus dans `LoDb.Api/Modules/` : `Catalog`, `Seo`, `Legacy`, `Accounts`,
`Profiles`, `Builds`, `Trends`, `PublicApi`, `Billing`, `Analytics`, `Audit`, `Admin`,
`Contact`, `ClientPolicy`. Features Angular prévues dans `src/app/features/` : `home`,
`context-switcher`, `catalogue` (`shared`, `champions`, `items`, `runes`, `summoners`),
`editorial`, `errors`, `account`, `profile`, `builds` (`mine`, `editor`, `share`,
`trends`, `shared`), `api-portal`, `developers`, `donate`, `contact`, `admin`.

## 4. Conventions de code

Elles s'ajoutent aux règles de `CLAUDE.md`, qui s'appliquent telles quelles : fichier
≤ 400 lignes (alerte à 300), ≤ 10 fichiers par dossier, fonction ≤ 30 lignes, ≤ 3
paramètres (constructeurs d'injection exemptés), imbrication ≤ 3, complexité ≤ 10, lignes
≤ 100 caractères, un élément public par fichier nommé comme lui, aucune constante magique,
commentaires en anglais qui expliquent le pourquoi. Elles sont reportées dans `CLAUDE.md`
au lot 0 (chantier L0.5).

### C#

- `net10.0`, `Nullable` activé, `TreatWarningsAsErrors`, `AnalysisLevel`
  `latest-recommended`, `ImplicitUsings`, espaces de noms déclarés au niveau fichier
  (`LoDb.<Projet>.<Dossier>`).
- Classes **`sealed`** par défaut ; `record` pour les DTO et les objets valeur ; membres
  `required` ; constructeurs primaires pour l'injection.
- Aucun état statique mutable. **`TimeProvider`** injecté plutôt que `DateTime.UtcNow`.
  `CancellationToken` sur toute méthode asynchrone.
- Journalisation par `[LoggerMessage]` généré : `EventName` = clé `domaine.sujet.resultat`
  ([`logging.md`](../guides/logging.md)), message à gabarit, exception passée en objet,
  **aucune clé de contexte `error`**, aucune donnée personnelle, une ligne de synthèse par
  lot.
- Erreurs : exceptions typées pour les erreurs transitoires ; l'absence définitive est une
  **valeur** (jamais une exception). `/api` répond en ProblemDetails, `/v1` garde son
  enveloppe `{error:{code,message}}`.
- Versions NuGet dans `Directory.Packages.props` uniquement, sans version flottante.

### TypeScript et Angular

- Angular 22 : composants standalone, signals, zoneless (défaut de la v22), `OnPush`,
  `inject()`, `input()`/`output()`, blocs `@if`/`@for`/`@defer`. TypeScript strict et
  templates stricts, pas de `any`.
- Un composant = présentation + câblage mince ; l'orchestration vit dans des services et
  des fonctions pures, testés sans monter le composant.
- ESLint (angular-eslint). Deux règles d'architecture bloquantes :
  - `@capacitor/*` et `@capawesome/*` ne s'importent que dans `src/app/core/platform/**` ;
  - une feature n'importe que `core/`, `ui/` et elle-même (ses sous-dossiers compris).
- Styles : jetons Hextech globaux, aucune couleur ni fonte en dur, propriétés logiques
  (`margin-inline`…) pour le RTL, champs de saisie à 16 px au moins.
- Tests : Vitest (`ng test`), fichiers `*.spec.ts` à côté du code.

### Commits

Conventions B-Hive inchangées : Conventional Commits en français, sujet à l'infinitif
≤ 72 caractères, un commit = un changement cohérent, tests dans le même commit, aucune
ligne d'attribution dans les messages. Carte des scopes de la nouvelle arborescence, à
ajouter à `CLAUDE.md` au lot 0 :

| Chemin | Scope |
|---|---|
| `src/LoDb.Domain/**` | `back/domain` |
| `src/LoDb.Ingestion/**` | `back/ingestion` |
| `src/LoDb.Infrastructure/Persistence/**` | `back/db` |
| `src/LoDb.Infrastructure/**` (autres) | `back/infrastructure` |
| `src/LoDb.Api/Modules/<Module>/**` | `back/<module>` (minuscules) |
| `src/LoDb.Api/**` (autres) | `back/host` |
| `src/LoDb.Web/src/app/features/<feature>/**` | `front/<feature>` |
| `src/LoDb.Web/src/app/{core,ui}/**`, `src/styles/**` | `front/core`, `front/ui` |
| `src/LoDb.Web/public/i18n/**` | `i18n` |
| `src/LoDb.Web/android/**` | `android` |
| `src/LoDb.Desktop/**` | `desktop` |
| `tests/LoDb.E2E/**` | `e2e` |
| `tests/LoDb.Parity/**`, `tests/fixtures/**` | scope du code testé |
| `docker/next/**`, `compose.next*` | `infra` |
| `tools/next/**` | `tools` |

Les entrées `docs/changelog/` décrivent ce qui arrive **en prod** : aucune n'est créée
pendant la reconstruction ; celles de la bascule sont rédigées au lot 8 (L8.3).

## 5. Décisions techniques transverses

Elles sont arrêtées pour que des chantiers parallèles produisent un code cohérent.

### 5.1 Backend

- **Enregistrement des services** : `Program.cs` (L0.1) appelle dès le départ :
  - les 14 modules, chacun exposant `Add<Module>(IServiceCollection, IConfiguration)` et
    `Map<Module>(IEndpointRouteBuilder)` ;
  - une méthode `AddLoDb<Zone>` par zone technique : `Egress`, `Storage`, `Persistence`,
    `Jobs` (verrous et base des tâches), `Ddragon`, `Ingestion`, `Catalog`, `Outbox`,
    `Audit`, `DataProtection`, `Analytics`.

  Chaque méthode vit dans un fichier **pré-créé vide** par L0.1 et appartient au chantier
  de sa zone. Les services hébergés (`LoDb.Api/Workers/<Zone>/`) et les sous-commandes
  (`LoDb.Api/Cli/<Zone>/`) sont **découverts par convention** dans l'assembly `LoDb.Api` :
  aucun fichier commun à modifier. `Program.cs` ne change plus ensuite.
- **Tâches de fond désactivables** : `LoDb:Workers:Enabled=false` coupe toutes les tâches
  de fond ; les tests d'intégration et la génération OpenAPI s'en servent (aucune veille de
  patch ne part sur le réseau pendant un test).
- **Aucune I/O ni exception** dans les `Add*` et `Map*` : la génération du document
  OpenAPI exécute `Program` sans démarrer l'hôte.
- **Minimal APIs** : `MapGroup` par module, `TypedResults` (OpenAPI précis), validation
  intégrée (`AddValidation`) sur des requêtes `record`.
- **OpenAPI** (`Microsoft.AspNetCore.OpenApi`) : deux documents, `app` pour `/api` (source
  du client TypeScript) et `public-v1` pour `/v1` (source de la page `/developers`).
  Générés **à la demande** dans `src/LoDb.Api/openapi/` (propriété MSBuild activée par le
  script `api:generate`), jamais à chaque build ; la CI les régénère et échoue s'ils
  diffèrent des fichiers commités.
- **JSON** : `System.Text.Json`, camelCase pour `/api` ; `/v1` garde exactement les noms
  de go-api (snake_case), figés par les tests de contrat.
- **Chemin canonique** d'une entité (`<type>/<id>[-<slug>]`) calculé une seule fois dans
  `LoDb.Domain` et renvoyé par l'API dans chaque liste et chaque détail. Sitemaps, 301
  héritées, analytics et front s'en servent ; le front n'y ajoute que `/{locale}` et
  `/{version}`.
- **Persistance** : EF Core 10 + Npgsql, `LoDbDbContext` unique, un
  `IEntityTypeConfiguration<T>` par entité, convention de nommage snake_case avec mapping
  explicite des noms Doctrine. La migration `Baseline` décrit le schéma Doctrine actuel.
  Les colonnes `timestamp without time zone` héritées, et elles seules (liste explicite),
  sont lues et écrites en UTC par un convertisseur ; tout nouveau champ est en
  `timestamptz`.
- **Migrations** : sous-commande `migrate` (L1.4). Sur une base Doctrine sans historique
  EF, elle marque d'abord `Baseline` comme appliquée, puis applique les migrations
  suivantes. En dev, un service Compose éphémère `migrate` passe avant `api` ; au
  déploiement, le même conteneur éphémère tourne **avant** la bascule du trafic.
- **Tâches de fond** : `BackgroundService` ; les tâches périodiques combinent
  `PeriodicTimer` et `IDistributedLock` (verrou consultatif Postgres
  `pg_try_advisory_lock`, une clé stable par tâche) ; les files sont des `Channel<T>`
  bornés. Chaque tâche publie durée, erreurs et profondeur de file, et une ligne de synthèse
  par lot.
- **Cache** : `HybridCache` (L1 seul) ; catalogue immuable par (version, langue) dans un
  LRU plafonné en mémoire.
- **Configuration** : options validées au démarrage (`ValidateOnStart`), section `LoDb`,
  variables d'environnement `LoDb__<Section>__<Clé>` ; aucun secret dans le dépôt,
  `.env.next.example` les documente.
- **Santé et métriques** : `/healthz` (vivant), `/readyz` (Postgres joignable, stockage
  inscriptible) ; OpenTelemetry, `/metrics` Prometheus sur le **port interne 9464**, jamais
  routé par nginx ; jauge `lodb_build_info{revision,version}` alimentée par `APP_REVISION`.
- **Sous-commandes** : `dotnet LoDb.Api.dll <commande>`, sans second binaire. Chaque
  commande implémente `ICliCommand` et vit sous `LoDb.Api/Cli/<Zone>/`, d'où la convention
  la découvre. Le choix de la commande se fait **avant** la construction de l'hôte web :
  `healthcheck` (sonde Docker de l'image *chiseled*, sans shell ni curl) n'ouvre ni 8080 ni
  9464 ; les autres commandes tournent dans un hôte générique sans Kestrel. Commandes
  prévues : `healthcheck`, `migrate`, `baseline mark-applied`, `ingest`, `catalog export`,
  `admin create`, `analytics import`, `audit import`, `client-policy publish`.
- **Audit** : `IAuditLog`, l'ensemble fermé des actions et l'écriture best effort (base +
  miroir dans les logs sans `ip` ni `meta.identifier`) sont livrés au lot 4 (L4.1) ; chaque
  chantier de module ajoute lui-même ses appels.
- **IP du client** : Caddy → nginx → API. nginx restaure l'IP réelle depuis le réseau de
  l'edge (`real_ip`, `LODB_EDGE_CIDR`) puis la transmet ; l'API ne fait confiance qu'au
  réseau de nginx (`ForwardedHeaders`). Toutes les limites par IP, le hash visiteur, la
  géolocalisation et l'audit en dépendent.
- **Sécurité** : CORS fermé sauf `https://localhost` (Android) sur `/api` et `*` en lecture
  sur `/v1` (comme go-api) ; antiforgery sur les requêtes non sûres authentifiées par
  cookie ; politiques de rate limiting nommées ; en-têtes constants (HSTS 2 ans,
  `nosniff`…) à l'edge nginx.

### 5.2 Front

- Workspace `src/LoDb.Web` (npm, `package-lock.json` commité), **deux configurations** :
  `web` (SSR, `outputMode: server`) et `shell` (navigateur seul, statique ; embarquée par
  Photino et Capacitor).
- **Origine de l'API** (jeton `API_BASE_URL`, origine sans chemin : les chemins générés
  commencent par `/api`) : côté SSR `http://api:8080`, avec `HTTP_TRANSFER_CACHE_ORIGIN_MAP`
  vers l'origine publique pour ne jamais charger deux fois ; navigateur web et desktop :
  même origine (le desktop passe par son proxy loopback) ; Android : origine publique
  fournie par l'environnement du build `shell`. La couche plateforme la choisit au
  démarrage.
- **Routage** : `path: ':locale'` + garde `canMatch` qui valide la locale (pas de `matcher`,
  incompatible avec le prérendu) ; segment `:version` optionnel, validé contre `/api/meta`
  (liste et motif fournis par l'API, jamais recopiés) ; routes paresseuses déclarées dans
  `app.routes.ts` dès L3.1 ; modes de rendu par route dans `app.routes.server.ts` (tableau
  de l'ADR 0005).
- **Prérendu** : une route prérendue n'appelle jamais l'API pendant son rendu ; ce qui en
  dépend (options du sélecteur de version, compteurs d'inventaire) se charge côté client.
- **Données** : services + signals (`httpResource`, `resource`), cache de transfert HTTP
  en SSR, jamais de double fetch.
- **i18n** : Transloco ; catalogues JSON `public/i18n/<locale>.json` et scopes
  `public/i18n/<scope>/<locale>.json` (`seo`, `api`, `about`, `admin`, puis un scope par
  feature pour toute clé nouvelle) ; chargeur **HttpClient** (URL relative), donc suivi par
  le rendu SSR et le prérendu, et repris par le cache de transfert ; pluriels ICU ; repli
  `en` par clé.
- **SEO** : `core/seo/` porte titre, meta, canonique, hreflang, robots et les constructeurs
  JSON-LD ; les pages ne font que leur passer des données.
- **Styles** : le front actuel est écrit sur **Tailwind v4** en configuration CSS
  (`@theme`, `@utility`, `@layer components`), sans PrimeVue. Tailwind v4 est conservé,
  branché par `@tailwindcss/postcss` (les styles de composant qui utilisent `@apply`
  déclarent `@reference`) ; aucun kit UI (ADR 0006) ; primitives accessibles par Angular
  CDK.
- **Plateforme** : jeton `PLATFORM` + interface `PlatformService` (lien externe,
  enregistrement de fichier, partage, état des mises à jour, en-tête client, origine de
  l'API, stratégie d'authentification) ; implémentations `web`, `desktop`, `android` dans
  `core/platform/`, détectées une fois au démarrage.
- **Authentification** (`core/auth/`, L4.5) : session lue sur `/api/account/me`, gardes,
  et interface `AuthStrategy` (connexion, déconnexion, session, préparation des requêtes)
  avec trois implémentations : `cookie` (web, L4.5), `host` (desktop : jetons tenus par
  l'hôte, L9.3), `bearer` (Android : jetons, refresh en stockage sécurisé, L10.2).
- **Thème** : cookie lu par un script inline du `<head>` qui pose `data-theme` avant le
  premier rendu ; le HTML ne dépend jamais du thème.
- **Gabarit racine** : le composant racine (L0.2) rend `<lodb-shell>` (`core/layout/`) qui
  projette les emplacements `[lodbSlot=switcher]`, `[lodbSlot=account]`,
  `[lodbSlot=banner]`, `[lodbSlot=contact]` et le `router-outlet`. L3.2 réalise l'enveloppe
  en gardant ces emplacements ; L3.1 y branche des composants provisoires aux chemins
  convenus, que les chantiers concernés remplacent sans toucher au gabarit.
- **Contrat de l'hôte desktop** (fixé ici pour que L9.2 et L9.3 avancent en parallèle) :
  - l'hôte injecte `window.__LODB_DESKTOP__ = {version}` dans l'`index.html` qu'il sert ;
  - endpoints locaux `POST /desktop/auth/login` (identifiant, mot de passe, « se souvenir
    de moi » → 204 ou 401), `POST /desktop/auth/logout`, `GET /desktop/auth/session`,
    `GET /desktop/auth/google` (ouvre le navigateur système) ;
  - pont : message JSON `{id, type, payload}` du JavaScript vers l'hôte, réponse
    `{id, ok, result | error}` ; types `openExternal {url}`, `saveFile {name, mime,
    base64}`, `updateState {}`, `applyUpdate {}` ; tout champ reçu est validé côté hôte.

### 5.3 Contrats partagés

Locales, correspondance locale → langue Data Dragon, motif de version, types de ressource,
éditions et chemins canoniques sont définis **une seule fois** dans `LoDb.Domain`, exposés
par OpenAPI (enums générés en tableaux, `enumArray`) ou par `/api/meta`, et consommés par le
client généré. Aucune copie écrite à la main côté TypeScript.

### 5.4 Conteneurs

| Image | Base | Utilisateur | Sonde |
|---|---|---|---|
| `lodb-api` | `mcr.microsoft.com/dotnet/aspnet:10.0` *chiseled* avec ICU | non root (`app`) | `dotnet LoDb.Api.dll healthcheck` |
| `lodb-web-ssr` | `node:24-slim` | `node` | `/healthz` du serveur SSR, via `node -e` |
| `lodb-nginx` | `nginx:1.31-alpine` | — | `/healthz` |

- Projet Compose `lodb-next` ; volumes `storage` (écrit par l'API seule, lu par nginx) et
  `pgdata` (dev) ; label OCI `org.opencontainers.image.revision` et argument
  `APP_REVISION` sur chaque image ; limites mémoire dans `compose.next.deploy.yaml` (règle :
  2 × le pic observé).
- SkiaSharp tourne dans l'image *chiseled* grâce à
  `SkiaSharp.NativeAssets.Linux.NoDependencies` (sans fontconfig) ; le transcodage est
  vérifié **dans le conteneur**, pas seulement sur l'hôte.
- nginx a deux blocs `server` : le site (tout le trafic public) et le sous-domaine
  `api.` (`API_CADDY_DOMAINS`), qui ne sert que `/v1/` comme go-api aujourd'hui.

## 6. Environnement de développement

### 6.1 Faits vérifiés (poste de dev, 2026-09-24)

- macOS arm64, 14 cœurs ; Docker Desktop 29, environ **11,7 Gio** pour les conteneurs,
  partagés avec les stacks d'autres projets qui tournent sur le poste.
- .NET SDK **10.0.400** (runtime 10.0.11) ; outils globaux `dotnet-ef` 10.0.11 et
  `vpk` 1.2.0.
- Node **26.5** / npm 12, accepté par Angular CLI 22 (`^22.22.3 || ^24.15.0 || >=26.0.0`).
  L'image SSR tourne sur Node 24 LTS.
- PHP 8.5 CLI avec argon2id, utile pour vérifier un hash à la main ; le test croisé de
  L4.1 lance `php:8.5-cli` en conteneur et ne dépend pas de l'hôte.
- Ni Go ni JDK ni SDK Android : les tests Go de l'ancienne stack passent par le conteneur
  `golang:1.26`, le build Android par un conteneur `linux/amd64` (L10.1), `aapt2` n'existant
  qu'en x86-64 sous Linux.
- Socket Docker par défaut présent (`/var/run/docker.sock`) : Testcontainers fonctionne
  sans variable d'environnement.
- Aucun hook Git dans le dépôt.

### 6.2 Commandes

À figer au lot 0 et à reporter dans `CLAUDE.md` (garde-fous de la nouvelle stack).

| Action | Commande | Où |
|---|---|---|
| Build .NET | `dotnet build LoDb.slnx -c Release` | hôte |
| Tests .NET | `dotnet test LoDb.slnx` (Testcontainers tire `postgres:17-alpine`) | hôte + Docker |
| Dépendances front | `npm ci --prefix src/LoDb.Web` | hôte |
| Front | `npm --prefix src/LoDb.Web run lint`, `typecheck`, `test`, `build:web`, `build:shell` | hôte |
| Contrat | `npm --prefix src/LoDb.Web run api:generate` (documents OpenAPI + client) | hôte |
| Stack `lodb-next` | `docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build` | Docker |
| Migrations | service `migrate` de la stack, ou `dotnet LoDb.Api.dll migrate` | Docker ou hôte |
| E2E | `npm --prefix tests/LoDb.E2E test` (stack démarrée) | hôte + stack |
| Ancienne stack | `docker compose up -d` à la racine, arrêtée après usage | Docker |

### 6.3 Ports hôte

L'ancienne stack (`lodb`) publie 8080, 8085, 8090, 5432, 8025 et 1025. D'autres projets
du poste occupent 3307, 33306 et 21200 à 21213. La stack `lodb-next` publie en dev des
ports réglables par variables d'environnement :

| Service | Variable | Stack d'intégration | Emplacement 1 | Emplacement 2 |
|---|---|---|---|---|
| nginx (point d'entrée) | `LODB_NEXT_HTTP_PORT` | 18080 | 18180 | 18280 |
| API en direct | `LODB_NEXT_API_PORT` | 18081 | 18181 | 18281 |
| SSR en direct | `LODB_NEXT_SSR_PORT` | 18082 | 18182 | 18282 |
| Postgres | `LODB_NEXT_PG_PORT` | 15432 | 15532 | 15632 |
| Mailpit (interface) | `LODB_NEXT_MAIL_PORT` | 18025 | 18125 | 18225 |

La stack d'intégration (`-p lodb-next`) tourne depuis la racine. Un chantier qui doit
vérifier une stack depuis son worktree prend un emplacement libre (`-p lodb-next-e1` ou
`-p lodb-next-e2`, ports ci-dessus) et la supprime avant de rendre. Les métriques (9464)
ne sont jamais publiées : on les lit depuis le réseau de la stack.

### 6.4 Ressources

- Au plus une stack d'intégration et deux stacks d'emplacement à la fois.
- Deux commandes qui écrivent dans la même base ou le même volume ne tournent jamais en
  parallèle.
- Les tâches lourdes (ancienne stack, build Android en conteneur, E2E) ne se chevauchent
  pas : ensemble, elles dépasseraient la mémoire disponible.

## 7. Ordre d'exécution

### 7.1 Graphe des lots

```
L0 ──► L1 ──► L2 ──► L3 fondations ──┬──► L3 pages ──► L3 E2E ──┐
 │      │                            │                          │
 │      └──► L4 (schéma dès L1) ─────┴──► L4 front              ├──► L8
 │                                         │                    │
 │                                         └──► L5, L6, L7 ─────┘
 ├──► L3.2 design system, L3.3 i18n (dès la fin du lot 0)
 ├──► L6.1 références /v1 (dès la fin du lot 0 : ancienne stack seule)
 └──► L9, L10 après les fondations de L3, L4.5 (auth front) et L6.2 (politique client)
```

### 7.2 Chantiers

| ID | Chantier | Taille | Dépend de |
|---|---|---|---|
| L0.1 | Solution .NET et hôte `LoDb.Api` (santé, logs, OTel, OpenAPI, CLI, enregistrements) | L | — |
| L0.2 | Workspace Angular (SSR + shell, Transloco, plateforme, gabarit, lint, Vitest) | L | — |
| L0.3 | Conteneurs et Compose `lodb-next` | M | L0.1, L0.2 |
| L0.4 | CI de la nouvelle stack, squelette E2E, workflow de déploiement `next` | M | L0.3 |
| L0.5 | `CLAUDE.md`, guide de dev de la nouvelle stack | S | L0.4 |
| L1.1 | Domaine Data Dragon pur (règles UP, éditions, faits dérivés, chemins) | L | lot 0 |
| L1.2 | Egress filtré (allow-list, redirections, plafond, résilience) | M | lot 0 |
| L1.3 | Stockage des contenus (`IBlobStore`, datasets, écritures atomiques) | M | lot 0 |
| L1.4 | Persistance : `LoDbDbContext`, `Baseline`, tables du lot 1, verrous, `migrate`, anonymisation | L | lot 0 |
| L1.5 | Clients DDragon / CDragon, normalisation, fixtures enregistrées | L | L1.1, L1.2 |
| L1.6 | Pipeline d'ingestion, veille de patch, WebP, files, tâches de fond | L | L1.3, L1.4, L1.5 |
| L1.7 | Catalogue en mémoire, résolution d'images | M | L1.6 |
| L1.8 | Parité PHP ↔ .NET (datasets et manifestes) | L | L1.6, L1.7 |
| L2.1 | Endpoints du catalogue (listes, détails, recherche, pickers, jumeaux) | L | lot 1 |
| L2.2 | Contrat OpenAPI, client TypeScript généré, contrôle de dérive | M | L2.1 |
| L3.1 | Routage localisé, résolution d'URL, coquille SSR | L | lot 2 |
| L3.2 | Design system Hextech, mise en page, thèmes, RTL | L | lot 0 |
| L3.3 | i18n : conversion YAML → JSON, Transloco SSR, complétude | M | lot 0 |
| L3.4 | SEO : services front, JSON-LD, sitemaps, robots, llms | L | lot 2 |
| L3.5 | Socle du catalogue (liste filtrable, facettes, état d'URL, pager) | M | L3.1 à L3.4 |
| L3.6 | Pages champions | L | L3.5 |
| L3.7 | Pages objets | M | L3.5 |
| L3.8 | Pages runes et sorts d'invocateur | M | L3.5 |
| L3.9 | Accueil et sélecteur de version et de langue | M | L3.1 à L3.4 |
| L3.10 | Pages éditoriales prérendues, changelog public | M | L3.1 à L3.4 |
| L3.11 | Cache HTTP nginx, PWA, en-têtes, durcissement SSR | M | L3.1 |
| L3.12 | Résolveur des 301 héritées | M | lot 2 |
| L3.13 | E2E, accessibilité, diff SEO face à la prod | L | L3.6 à L3.12 |
| L4.1 | Schéma Identity, hasher, politique de mot de passe, contrats outbox et audit | L | lot 1 |
| L4.2 | Authentification web et jetons, Google, protections | L | L4.1 |
| L4.3 | Outbox e-mail et gabarits localisés | M | L4.1 |
| L4.4 | Profil, favoris, profil public, suppression, bannissement (API) | M | L4.2, lot 2 |
| L4.5 | Socle d'authentification front (`core/auth`, stratégie cookie) | M | L4.2, fondations L3 |
| L4.6 | Pages compte et profil | L | L4.4, L4.5 |
| L5.1 | Règles et API des builds, votes, import | L | lot 4 |
| L5.2 | Éditeur de builds et « mes builds » | L | L5.1 |
| L5.3 | Partage `/b/{token}`, votes, page tendances | M | L5.1 |
| L6.1 | Références de contrat `/v1` capturées sur go-api | M | lot 0 |
| L6.2 | Schéma des lots 6, 7, 9 et 10 | M | lot 4 |
| L6.3 | Module `/v1` (clés, rate limit, quotas, crédits, métrage, tendances) | L | L6.1, L6.2 |
| L6.4 | Portail des clés et page `/developers` | M | L6.3 |
| L6.5 | Stripe : checkout, webhooks idempotents, expiration des crédits, dons | L | L6.2 |
| L7.1 | Analytics : capture, partitions, rollups, rétention, reprise | L | L6.2 |
| L7.2 | Audit : rétention, requêtes, purge, reprise de l'historique | M | lot 4 |
| L7.3 | API admin : rôle, MFA TOTP, premier admin, panneaux | L | L6.3, L7.1, L7.2 |
| L7.4 | Front admin | L | L7.3 |
| L7.5 | Contact | S | lot 4 |
| L8.1 | Déploiement : compose de prod, CD, migrations avant bascule | M | lots 3 à 7 |
| L8.2 | Outils de bascule (pré-ingestion, 301, smoke tests, surveillance) | M | lots 3 à 7 |
| L8.3 | Runbook de bascule, retour arrière, contract, changelog joueurs | M | L8.1, L8.2 |
| L9.0 | Politique client : `/api/client-policy`, `X-LoDb-Client`, `426` | M | L6.2, fondations L3 |
| L9.1 | Spike Photino / Avalonia | S | fondations L3 |
| L9.2 | Hôte desktop (Kestrel loopback, proxy YARP, pont, jetons) | L | L9.1, L4.2 |
| L9.3 | Plateforme desktop côté front | M | L9.0, L9.1, L4.5 |
| L9.4 | Mises à jour Velopack et release desktop | L | L9.2 |
| L10.1 | Projet Capacitor Android | M | fondations L3 |
| L10.2 | Plateforme Android côté front (plugins, jetons, App Links) | M | L10.1, L4.5 |
| L10.3 | Live update signé et Play In-App Updates | L | L9.0, L10.2 |
| L10.4 | Build et release Android | M | L10.3 |

« Lot n » en dépendance signifie : après le jalon du lot n. « Fondations L3 » signifie :
après l'intégration de L3.1 à L3.4. Les chantiers front d'un lot (L5.2, L6.4…) supposent
aussi les fondations du lot 3, acquises avant le lot 4.

### 7.3 Fichiers partagés

| Fichier | Propriétaire | Règle pour les autres |
|---|---|---|
| `LoDb.slnx`, `global.json`, `Directory.Build.props` | L0.1 | un nouveau projet se déclare au compte rendu ; l'intégration l'ajoute |
| `Directory.Packages.props` | L0.1 | ajouts de paquets permis, conflits résolus à l'intégration |
| `src/LoDb.Api/Program.cs` | L0.1 | ne change plus : modules et zones sont pré-enregistrés |
| Fichiers `Add<Zone>` pré-créés | le chantier de la zone (§5.1) | chacun ne remplit que le sien |
| `src/LoDb.Api/Hosting/**` | L0.1 | L4.2 y ajoute les politiques de rate limiting et d'autorisation |
| `Persistence/**` (entités, configurations, migrations) | chantier de schéma du lot (L1.4, L4.1, L6.2) | personne d'autre ne les modifie |
| `src/LoDb.Web/package.json`, `package-lock.json`, `angular.json` | L0.2 | scripts `api:generate`, `api:check`, `i18n:convert`, `i18n:report` pré-déclarés ; ajouts de dépendances permis, lockfile régénéré à l'intégration |
| Composant racine | L0.2, puis L3.1 | L3.1 y branche les composants provisoires ; ensuite il ne change plus |
| `core/layout/**` (dont `<lodb-shell>`) | L3.2 | emplacements du §5.2 conservés |
| `src/LoDb.Web/src/server.ts` | L0.2, puis L3.1 (`/` → 302), puis L3.11 (durcissement) | — |
| `app.routes.ts`, `app.routes.server.ts` | L3.1 | chaque feature remplace son `<feature>.routes.ts` et ses composants provisoires |
| `core/auth/**` | L4.5 | L9.3 et L10.2 ajoutent leur stratégie dans `core/platform/` |
| `public/i18n/*.json` convertis | L3.3 | une clé nouvelle va dans le scope de sa feature |
| `openapi/*.json`, client généré | jamais édités à la main | régénérés et commités par L2.2 puis par les intégrations |
| `compose.next*.yaml`, `docker/next/**` | L0.3, puis L3.11 (nginx), L8.1 (déploiement) | réservés : service `migrate` (L1.4), `nginx/server.d/seo.conf` (L3.4), `nginx/server.d/legacy-redirects.conf` (L3.12), `nginx/server.d/analytics.conf` et `nginx/snippets/analytics-mirror.conf` (L7.1), `android-build/**` (L10.1) |
| `.github/workflows/next-*.yml`, `.github/dependabot.yml` | L0.4 | jobs `contract` et `i18n` pré-déclarés (appellent les scripts npm) ; L8.1, L9.4 et L10.4 ont leurs propres fichiers |
| `tests/LoDb.E2E/` (configuration, `specs/public/`) | L0.4, puis L3.13 | chaque chantier front écrit ses specs dans `specs/<feature>/` |
| `CLAUDE.md` | L0.5 | les jalons suivants n'y ajoutent que leurs garde-fous |

## 8. Jalons et critères de sortie

Chaque lot se clôt par un jalon qui :

1. fusionne les branches du lot dans la branche d'intégration et résout les conflits ;
2. régénère et committe ce qui est généré (documents OpenAPI, client TypeScript,
   lockfile) ;
3. lance les suites complètes : `dotnet test`, front (`lint`, `typecheck`, `test`,
   `build:web`, `build:shell`), contrôle de dérive du contrat, puis, dès le lot 3, la stack
   `lodb-next` reconstruite depuis la racine et les E2E ;
4. vérifie le critère de sortie local du lot (tableau ci-dessous) ;
5. consigne les échecs dans un rapport, les fait corriger, puis relance la vérification.

| Lot | Critère de sortie local |
|---|---|
| 0 | Suites vertes ; `/en/` rendu en SSR par la stack `lodb-next` ; `/healthz`, `/readyz` et `lodb_build_info` lisibles ; logs JSON d'une ligne |
| 1 | Parité des datasets et manifestes normalisés, PHP vs .NET, sur les 10 dernières versions × 5 langues + les versions pièges (rapport) ; un patch complet ingéré sans intervention, WebP produit dans le conteneur |
| 2 | Contrat OpenAPI stable ; client généré commité ; contrôle de dérive actif |
| 3 | Diff SEO sur un échantillon face à la prod (rapport) ; E2E et accessibilité verts ; budgets Lighthouse tenus sur la stack locale (performance ≥ 90, accessibilité ≥ 95, SEO ≥ 95, LCP ≤ 2,5 s, CLS ≤ 0,1), comparaison à la prod à titre indicatif |
| 4 | Connexion de comptes bcrypt et argon2 (hash au format produit par Symfony) ; hash ré-écrit vérifié par `password_verify` de PHP |
| 5 | Builds de l'ancienne base affichés à l'identique, fantômes compris : faits extraits du DOM (champion, patch, mode, runes, objets par étape, fantômes) comparés automatiquement entre les deux stacks sur la même base |
| 6 | Tests de contrat `/v1` verts face aux références de go-api ; webhook rejoué sans double crédit |
| 7 | Chaque panneau et chaque action admin exercés par E2E ; totaux des panneaux égaux entre les deux stacks sur les mêmes agrégats ; purge de rétention observée (horloge simulée) |
| 8 | Répétition locale : base de l'ancienne stack migrée, nouvelle stack servie dessus, E2E en lecture seule, puis **ancienne stack relancée sur le même schéma** (retour arrière prouvé) |
| 9 | Build desktop macOS arm64 ; `--smoke` affiche sa version ; mise à jour N-1 → N observée en local, non signée, par `--smoke` avant et après |
| 10 | APK de debug construit en conteneur `linux/amd64` ; live update, retour arrière d'un bundle défectueux et politique `426` couverts par tests |

## 9. Définition de terminé

- Tous les chantiers livrés, fusionnés dans la branche d'intégration, arbre de travail
  propre.
- Suites .NET, front, contrat et E2E vertes ; critères de sortie locaux des lots 0 à 10
  vérifiés.
- Rapports dans `docs/reecriture/rapports/` : schéma de base (lot 1), parité (lot 1), diff
  SEO et Lighthouse (lot 3), complétude i18n, écarts assumés du contrat `/v1` (lot 6),
  spike desktop (lot 9).
- Runbook de bascule et liste des opérations hôte à jour (L8.3).
- L'ancienne stack démarre et ses tests passent comme avant.

## 10. Couverture

### 10.1 `heritage.md` → chantiers

| Section | Points → chantier |
|---|---|
| § 1 FPM | A1, A9 → L1.6 · A2 → L7.1 · A3 → L1.6 · A4, G6 → L4.2 · A5 → L1.2 · A6 → L6.3 · A7 → L1.7 · A8, E1, E2, E5 → L0.1 · F4 → L0.3 · F5 → L0.1, L0.2 · I3 → L4.3 · J3 → disparaît |
| § 2 Architecture | A10 → L1.6, L6.5, L7.1, L7.2 · B1 → L1.6 · B2 → L1.4 · B3, B5 → L1.3 · B4 → L0.3, L1.3 · C1, C2 → L3.1 · C3 → L3.5 à L3.8 · C4 → L3.11 · C5 → L1.6 · D1 → L0.3, L1.4 · D2 → L7.1 · D3 → L4.1, L7.1, L7.2 · E3, E4 → L0.1 · F1, F2 → L8.1 · F3 → L1.4, L8.1 · G1 → L1.2 · G2 → L3.11 · G3 → L7.3 · G4 → L6.5 · G5 → L4.2 · I1 → L3.3 · I2 → L3.1, L3.3 · J1 → L0.4 · J2 → L1.1, L2.2 |
| § 3 Front | H1, H3 → L3.6 · H2 → L3.5 · H4 → L3.11 · H5 → L7.4 · H6 → L3.2, L3.13 |
| § 4 Bugs latents | hors plan (corrigés sur `dev`) ; non reproduits : cache d'un échec transitoire → L1.5, L1.7 ; HSTS → L3.11 ; sentinelles de portée → L1.1 |
| § 5 Particularités UP | 1 à 13 → L1.1 (règles), L1.5 (fetch et normalisation), L1.7 (images, hotlink) |
| § 6 Invariants | Indexation → L1.1 · Absences → L1.5, L1.6 · Stockage → L1.3 · Synchronisme → L1.7, L2.1 · URLs → L1.1, L3.1, L5.3 · Builds → L5.1 · Favoris → L4.4 · Confidentialité → L4.2 (retour au `Referer` du même hôte), L4.4, L5.1 · API publique → L6.3, L6.4 · Stripe → L6.5 · Audit → L4.1, L7.2 · Préférences → L3.1, L3.2 · SEO → L3.4 |

### 10.2 ADR → chantiers

| ADR | Chantiers |
|---|---|
| 0001 Réécriture | L0.1, L0.2, L0.5 |
| 0002 Hôte unique | L0.1, L1.2, L6.3 |
| 0003 Ingestion et tâches de fond | L1.6, L1.7, L4.3, L6.5, L7.1, L7.2 |
| 0004 Stockage et état | L1.3, L1.4, L1.7, L4.1, L6.2, L7.1, L9.0 |
| 0005 SSR, URLs, SEO | L3.1, L3.4, L3.10, L3.11, L3.12 |
| 0006 Front multi-cibles | L0.2, L2.2, L3.2, L3.3, L4.5 |
| 0007 Coquilles | L9.1, L9.2, L10.1, L10.2 |
| 0008 Mises à jour | L9.0, L9.4, L10.3, L10.4 |
| 0009 Identité | L4.1, L4.2, L4.5, L7.3 |
| 0010 Observabilité, tests, CI | L0.1, L0.4, L1.8, L3.13, L6.1 |

## 11. Opérations hôte et humaines

Ce qui ne se fait pas dans le dépôt, placé au moment où cela bloque.

| Opération | Nécessaire pour | Quand |
|---|---|---|
| Accès réseau à `ddragon.leagueoflegends.com` et `raw.communitydragon.org` | fixtures (L1.5), parité (L1.8) | dès le lot 1 |
| Accès réseau au site en prod (lecture seule) | diff SEO (L3.13) | lot 3 |
| Mémoire Docker libre (arrêter les stacks inutiles du poste) | ancienne stack + nouvelle stack | avant L1.4, L1.8, L6.1 |
| Émulation Rosetta active dans Docker Desktop (images `linux/amd64`) | build Android en conteneur (L10.1) | avant le lot 10 |
| Pousser la branche, ouvrir les PR | CI GitHub réelle | à la main, à chaque jalon souhaité |
| Déployer `next` : sous-domaine DNS, labels Caddy (`infra-vps`), secrets d'environnement GitHub, dump anonymisé (outil de L1.4), scrape `/metrics` et tableaux Grafana | critères « sur `next` » des lots 0 à 7 | à partir du lot 0, en continu |
| Clés Stripe de test, client OAuth Google (web, Android, desktop) avec l'URI de retour `/api/account/google/callback`, SMTP, certificat Data Protection | essais réels (les tests utilisent des doublures) | avant la répétition de bascule |
| Apple Developer ID + profil de notarisation, abonnement Azure Artifact Signing | release desktop signée (L9.4) | avant l'ouverture publique des mises à jour |
| Compte Play Console, clé d'envoi, clé RSA des bundles (sauvegarde hors ligne), vérification développeur, empreinte du certificat pour `assetlinks.json` | release Android (L10.4) | avant la piste de test interne |
| Répétition de bascule avec un dump réel chiffré, fenêtre de bascule, Search Console, surveillance 72 h, *contract* après 30 jours, décommission | lot 8 | suivant le [runbook](plan-migration.md#bascule-lot-8) |

## 12. Risques d'exécution

| Risque | Parade |
|---|---|
| Conflits de fusion sur les fichiers communs | propriétaires désignés (§7.3), modules, zones et routes pré-déclarés, migrations produites par un seul chantier par lot, artefacts générés seulement à l'intégration |
| Data Dragon lent ou indisponible pendant les tests | fixtures enregistrées rejouées ; seules la parité et l'enregistrement touchent le réseau |
| Mémoire Docker saturée | une stack d'intégration, deux emplacements au plus, tâches lourdes en série (§6.4) |
| Stack partagée écrasée par un autre agent | noms de projet et ports par emplacement (§6.3) ; seuls l'intégration et les jalons touchent `lodb-next` |
| Dérive entre back et front | client généré, contrôle de dérive dès L2.2, chemins canoniques calculés par l'API |
| Photino défaillant | spike L9.1 avant tout code desktop ; `IDesktopShell` et plan B Avalonia |
| Parité incomplète découverte tard | parité dès le lot 1, tests de contrat `/v1` dès que L6.1 a capturé les références |
| Retour arrière impossible | migrations additives, test croisé des hash, répétition L8 qui relance l'ancienne stack sur le schéma migré |

## 13. Écarts assumés

| Sujet | Décision du plan | Référence | Raison |
|---|---|---|---|
| `/b/{token}` | toujours `noindex`, sans JSON-LD | conforme à l'ADR 0005 et `heritage.md` § 6 ; l'actuel indexe les builds publics | capacité opaque, pas une page de contenu |
| Migrations au déploiement | conteneur éphémère de l'image `lodb-api` (`migrate`) plutôt qu'`efbundle` | ADR 0010 | même garantie (avant la bascule du trafic), une image de moins |
| Endpoints de compte et de jetons | endpoints propres plutôt que `MapIdentityApi` | ADR 0009 | connexion par e-mail **ou** nom d'utilisateur, politique CNIL, bannissement, locale explicite |
| Capture analytics | `mirror` nginx pour les pages servies + balise du routeur pour les navigations internes, plutôt qu'un middleware de l'API | ADR 0003, `heritage.md` A2 | les pages sont servies par le SSR et le cache nginx, que l'API ne voit pas |
| Pages éditoriales prérendues | compteurs d'inventaire de `/about` et `/about/data` chargés côté client | ADR 0005 | un prérendu ne peut pas appeler l'API au build |
| Styles | Tailwind v4 conservé | ADR 0006 (qui cite PrimeVue, absent du code) | c'est le socle réel du design system actuel |
| Ingestion de la longue traîne | pas de flux SSE en v1 : placeholders + une relance après `Retry-After` | ADR 0003 (« si une interface d'attente reste utile ») | moins de code, aucune boucle de polling |
