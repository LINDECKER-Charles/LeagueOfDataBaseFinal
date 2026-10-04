# LeagueOfDataBase — instructions projet

Encyclopédie League of Legends (champions, objets, runes, sorts d'invocateur) pour chaque
version et chaque langue de **Data Dragon**, plus CommunityDragon pour les chromas. Un seul
front sert le web, le desktop et Android. Ce fichier fixe l'architecture, les règles de code
et les conventions du dépôt ; il complète les préférences globales de l'agent sans les
redire.

L'ancienne version (Symfony + Go + Vue) est **archivée sous `legacy/`** ; ses règles sont
dans [`legacy/AGENTS.md`](legacy/AGENTS.md). On n'y touche pas, sauf correctif exigé par la
prod avant la bascule ([runbook](docs/reecriture/bascule.md)).

## Changelog : une entrée par changement visible

Chaque feature, correctif ou gain perceptible par le joueur crée une entrée
`docs/changelog/YYYY/YYYY-MM-DD-slug.md` (date de livraison) au format de
[`TEMPLATE.md`](docs/changelog/TEMPLATE.md) : frontmatter `date`, `type`, `scope` (`front`,
`back`, `apps`, `infra`, `full-stack`), `title`, `summary`, `tags` ; corps orienté joueur,
contexte technique sous `## Technique`.

- ✅ feature, bug visible, perf ou UX perceptible, devops visible (disponibilité, sécurité).
- ❌ refacto interne, tests, lint, formatage, dépendances sans effet.
- L'entrée part **dans le commit** du changement ; deux changements, deux entrées.
- Release : synthèse manuelle vers
  `src/LoDb.Web/src/app/features/editorial/changelog/published/`, puis archivage dans
  `docs/changelog/archived/YYYY/` ([`README`](docs/changelog/README.md)). Les entrées de la
  bascule sont des brouillons : [`changelog-bascule/`](docs/reecriture/changelog-bascule/README.md).

## Stack

| Couche | Techno |
|---|---|
| API, ingestion, tâches de fond | ASP.NET Core 10, un seul hôte `LoDb.Api` (`/api`, `/v1`, `/webhooks`) |
| Données | PostgreSQL 17 + EF Core ; blobs Data Dragon adressés par contenu (volume `ddragon`) |
| Web | Angular 22 en SSR (Node 24), locale dans l'URL (`/{locale}/…`), 21 langues |
| Desktop / Android | Photino + Velopack / Capacitor 8 + live update signé |
| Frontal | nginx (cache des pages, `/cdn/blobs/`) derrière l'edge Caddy du VPS (`infra-vps`) |
| Observabilité | OpenTelemetry, logs JSON d'une ligne, `/metrics` (port 9464) |

Décisions : [`docs/reecriture/README.md`](docs/reecriture/README.md) et ses ADR 0001 à 0010.

```
compose.yaml  compose.override.yaml (dev)  compose.deploy.yaml (hôtes)
.env.staging.example  .env.prod.example        docker/{api,web-ssr,nginx,android-build}/
src/LoDb.Domain/          pur, sans I/O : versions, langues, éditions, chemins canoniques
src/LoDb.Ingestion/       Egress/ Ddragon/ Pipeline/ Catalog/ …
src/LoDb.Infrastructure/  Persistence/ Storage/ Jobs/ Outbox/ Audit/ Analytics/ DataProtection/
src/LoDb.Api/             Program.cs Hosting/ Cli/<Zone>/ Workers/<Zone>/ Modules/<M>/ openapi/
src/LoDb.Web/             Angular : src/app/{core,ui,features}  src/server/  public/i18n/
src/LoDb.Desktop/         coquille desktop
tests/LoDb.*.Tests/  tests/LoDb.Testing/  tests/LoDb.E2E/  tests/LoDb.Parity/  tests/fixtures/
tools/                    contrat, i18n, fixtures, releases, bascule, parité
```

Références : `Api` → `Ingestion` → `Infrastructure` → `Domain`. Les modules
(`Add<Module>`/`Map<Module>`) et les zones (`AddLoDb<Zone>`) sont pré-enregistrés :
`Program.cs` ne change pas, un chantier ne remplit que son fichier.

### Environnements

| Env | Projet Compose, tags | `.env` → secret | Déploiement (`ci.yml`) |
|---|---|---|---|
| local | `lodb-dev` (emplacements `-e1`, `-e2`) | aucun | `docker compose` sur le poste |
| staging | `lodb-staging`, `:staging` | `.env.staging` → `ENV_STAGING` | push `dev` → checks → merge `dev`→`test` → build → `test.league-of-data-base.com` |
| prod | `lodb-prod`, `:prod` `:latest` | `.env.prod` → `ENV_PROD` | merge manuel `test`→`main` → retag `:staging`→`:prod`, sans rebuild |

Hôtes : secrets `STAGING_*` / `PROD_*` (`SSH_KEY`, `HOST`, `PATH`, `SSH_USER`,
`DATA_PROTECTION_PFX`). La stack prend la place de l'ancienne dans le même projet : volume
`pgdata` repris et migré par `migrate`, blobs dans `ddragon` (l'ancien `storage` reste
intact). Détail : [`configuration.md`](docs/guides/configuration.md).

## Invariants d'architecture (ADR)

- **Un seul hôte `LoDb.Api`** (ADR 0002). L'egress Data Dragon et CommunityDragon passe
  par le client nommé `ddragon` et son allow-list (https, deux hôtes, redirections
  re-vérifiées, plafond de taille). 403/404 = **absence définitive**, rendue comme une
  valeur ; 5xx/timeout = transitoire, jamais persisté.
- **`Add*` et `Map*` ne font aucune I/O et ne lèvent rien** (la génération OpenAPI exécute
  `Program` sans configuration). Sous-commandes `ICliCommand` sous `Cli/<Zone>/`, tâches
  `BackgroundService` sous `Workers/<Zone>/`, découvertes par convention ;
  `LoDb:Workers:Enabled=false` coupe toutes les tâches.
- **Tâches périodiques** (ADR 0003) : `PeriodicTimer` + verrou consultatif Postgres, files
  `Channel<T>` bornées, **une ligne de synthèse par lot**, jamais une par URL.
- **Stockage** (ADR 0004) : blobs derrière `IBlobStore`, adressés par contenu, écrits de
  façon **atomique** (temporaire sur le même volume, puis `File.Move`) ; nginx ne sert que
  `/cdn/blobs/`. Manifeste, analytics, audit et clés Data Protection en Postgres
  (`absent` = absence définitive). **Aucune session serveur**.
- **Objets et sorts indexés par id, jamais par nom** (jumeaux homonymes de l'édition Classic).
- **SSR** (ADR 0005) : **aucun cookie** lu, **aucun secret** détenu. Locale en préfixe
  d'URL ; l'id fait foi, le slug est décoratif (301 vers la canonique).
- **Front** (ADR 0006) : uniquement le **client généré** depuis OpenAPI. Aucun
  `if (platform)` hors de `core/platform/`, aucun kit UI, pas de NgRx.
- **Persistance** : migrations EF **additives** tant qu'un retour à l'ancienne stack reste
  possible, jusqu'au *contract* (runbook § 9). Une base Doctrine est d'abord marquée à
  `Baseline` par `migrate`.
- **Identité** (ADR 0009) : hash argon2id PHC, lisibles par `password_verify` de PHP.
  Jamais `AllowAnonymous` : les politiques portent le garde XSRF et `Origin`.
- **Observabilité** (ADR 0010) : logs JSON d'**une ligne** avec `trace_id`, `EventName` en
  `domaine.sujet.resultat`, **aucune clé `error`**, aucune donnée personnelle. `/metrics`
  sur 9464, **jamais routé ni publié**. `/healthz` ne dépend de rien ; `/readyz` vérifie
  Postgres et le stockage.
- **Apps** (ADR 0007, 0008) : Photino derrière `IDesktopShell` ; Capacitor 8, live update
  signé ; `X-LoDb-Client` et `426` : l'API décide.
- **Artefacts générés** (`openapi/*.json`, `core/api/generated/`, `package-lock.json`) :
  jamais édités à la main ; `npm run api:generate`, inclus dans le commit qui change le
  contrat.

## Conventions de code

Limites = plafonds ; principes = défauts à suivre sauf raison explicite.

- **DRY** (pas d'abstraction avant la 3ᵉ répétition), **KISS**, **SOLID**, **CQS** (modifier
  l'état **ou** retourner une valeur).

| Fichier | Fichiers / dossier | Fonction | Paramètres | Imbrication | Complexité | Ligne |
|---|---|---|---|---|---|---|
| ≤ 300 (alerte), 400 max | ≤ 10 | ≤ 30 lignes | ≤ 3 (injection exemptée) | ≤ 3 | ≤ 10 | ≤ 100 car. |

Exceptions : dossiers de données (releases du changelog, fixtures, catalogues i18n) et
racine de `src/LoDb.Web/` (fichiers imposés par l'outillage).

- **Un seul élément public par fichier**, nommé comme lui ; pas de nombre ni de chaîne
  magique (constantes nommées).
- Noms révélant l'intention, pas d'abréviation cryptique (`id`, `url`, `http`, `sha`
  tolérés), booléens en `is`/`has`/`should`/`can`.
- Une fonction fait une chose ; pures par défaut ; *guard clauses* ; pas de paramètre
  drapeau (sauf opt-in orthogonal documenté).
- **Commentaires en anglais**, sur le *pourquoi*, jamais la paraphrase du code.

**C# (.NET 10)** — `Nullable`, `TreatWarningsAsErrors`, `AnalysisLevel`
`latest-recommended` : **zéro avertissement**. Espaces de noms de fichier
(`LoDb.<Projet>.<Dossier>`). Classes `sealed`, `record` pour DTO et objets valeur, membres
`required`, constructeurs primaires. Aucun état statique mutable : `TimeProvider` injecté,
`CancellationToken` partout, async de bout en bout. `[LoggerMessage]`, exception en objet ;
exceptions typées pour le transitoire, l'absence définitive est une valeur. `/api` en
ProblemDetails, `/v1` en `{error:{code,message}}` snake_case. Minimal APIs (`MapGroup`,
`TypedResults`), options `LoDb` validées au démarrage, configuration par
`LoDb__<Section>__<Clé>`, jamais de secret dans `appsettings*.json`. Versions NuGet dans
`Directory.Packages.props` seul.

**TypeScript / Angular 22** — standalone, signals, zoneless, `OnPush`, `inject()`,
`input()`/`output()`, `@if`/`@for`/`@defer` ; strict, pas de `any`. Un composant =
présentation + câblage mince, l'orchestration dans des services et fonctions pures. ESLint
bloquant : `@capacitor/*` et `@capawesome/*` seulement sous `core/platform/` ; une feature
n'importe que `core/`, `ui/` et elle-même. Styles : jetons Hextech, propriétés logiques
(RTL), champs à 16 px au moins. Une page ne touche jamais au `<head>` :
`inject(Seo).apply(SeoPage)`. Liens avec query ou ancre par `UrlTree`
(`injectCatalogueLink`), jamais une chaîne dans `[routerLink]`.

**Tests** — toute feature livre ses tests dans le même commit : nominal et cas limites
introduits, rien de plus ; un test casse quand le comportement casse. xUnit v3 dans
`tests/LoDb.<Projet>.Tests/` (Postgres réel par Testcontainers, Data Dragon rejoué depuis
`tests/fixtures/`) ; Vitest en `*.spec.ts` à côté du code ; E2E Playwright dans
`tests/LoDb.E2E/specs/<feature>/` ; clés i18n dans `public/i18n/<feature>/` ; outils en
`node --test` dans `tools/<outil>/test/`.

## Garde-fous (avant de rendre un travail)

```bash
dotnet build LoDb.slnx -c Release            # 0 avertissement
dotnet test LoDb.slnx                        # Testcontainers : Docker démarré
npm ci --prefix src/LoDb.Web
npm --prefix src/LoDb.Web run lint           # puis typecheck, test, build:web, build:shell
npm --prefix src/LoDb.Web run api:check      # le client généré suit le contrat
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml up -d --build --wait
npm --prefix tests/LoDb.E2E test             # stack démarrée (LODB_E2E_BASE_URL sinon)
bash tools/routing/check-urls.sh             # grammaire d'URL de l'ADR 0005
```

Selon ce qui est touché : `node --test 'tools/<outil>/test/*.test.mjs'` ;
`docker run --rm -v "$PWD:/repo" -w /repo rhysd/actionlint` pour les workflows ;
`docker compose --env-file .env.staging.example -f compose.yaml -f compose.deploy.yaml
config --quiet` pour la surcouche des hôtes. Détail :
[`developpement.md`](docs/guides/developpement.md).

- **Stack locale `lodb-dev`** : 18080 nginx, 18081 API, 18082 SSR, 15432 Postgres, 18025
  Mailpit. Emplacements `lodb-dev-e1`/`-e2` (181xx, 182xx) pour un worktree, supprimés
  (`down -v`) avant de rendre.
- **Ne jamais toucher** aux conteneurs `chewb-*`, `laforce-*` et `grafana/mcp-grafana`, ni à
  leurs ports (3307, 33306, 21200 à 21213). Le port 9464 n'est jamais publié.
- **Pièges connus** (comportements voulus, à ne pas « corriger ») :
  [`docs/architecture/pieges.md`](docs/architecture/pieges.md). À lire avant de toucher au
  SSR, aux E2E, à nginx ou au déploiement.

## commit

Conventional Commits en **français**, sujet à l'infinitif, sans accents, **72 caractères
au plus**, corps réservé au *pourquoi*, pied `BREAKING CHANGE:` si besoin. Un commit = un
changement cohérent ; tests et entrée de changelog dans le même commit ; `git add` par
chemins explicites ; aucune ligne d'attribution. Branches : `type/description-courte`.

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
| `…/features/editorial/changelog/published/**` (+ archivage `docs/changelog/**`) | `changelog` |
| `src/LoDb.Web/public/i18n/**` | `i18n` |
| `src/LoDb.Web/android/**` | `android` |
| `src/LoDb.Desktop/**` | `desktop` |
| `tests/LoDb.E2E/**` | `e2e` |
| `tests/LoDb.*.Tests/**`, `tests/LoDb.Parity/**`, `tests/fixtures/**` | scope du code testé |
| `docker/**`, `compose*.yaml`, `.env.*.example` | `infra` |
| `tools/**` | `tools` |
| `.github/**` | `ci` |
| `docs/**`, `README.md`, `CONTRIBUTING.md`, `AGENTS.md` | `docs` |
| `legacy/**` | `legacy` |
| `LoDb.slnx`, `Directory.*.props`, `global.json`, `.editorconfig`, lockfiles | sans scope (`build` ou `chore`) |

## Références

[`developpement.md`](docs/guides/developpement.md) (commandes, ports, dépannage) ·
[`configuration.md`](docs/guides/configuration.md) (secrets et `.env`) ·
[`github-actions-secrets.md`](docs/guides/github-actions-secrets.md) (pipeline) ·
[`bascule.md`](docs/reecriture/bascule.md) (bascule, retour arrière, *contract*) ·
[`observabilite.md`](docs/guides/observabilite.md) ·
[`release-desktop.md`](docs/guides/release-desktop.md) ·
[`release-android.md`](docs/guides/release-android.md).
