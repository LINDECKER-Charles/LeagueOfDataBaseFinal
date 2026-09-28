# LeagueOfDataBase — instructions projet

Encyclopédie League of Legends servie depuis les données **Data Dragon** (+ CommunityDragon pour les chromas). Ce fichier fixe l'architecture et les **règles de code** du dépôt. Il complète les préférences globales `~/.claude/CLAUDE.md` (senior, concis, production-grade) — ne pas les redupliquer.

## 🔁 Règle critique : versionner chaque feature / fix dans `docs/changelog/`

**À chaque feature implémentée ou bug corrigé, créer une entrée dédiée dans `docs/changelog/`.**

Ce répertoire est le **journal technique interne** : source de vérité de tout ce qui a été
touché, jamais filtré. Aucune entrée = changement invisible.

> **Exception : la nouvelle stack** (`src/`, `tests/`, `docker/next/`, `compose.next*`,
> `tools/next/`). Elle ne crée **aucune** entrée pendant la reconstruction : ces entrées
> décrivent ce qui arrive en prod, et celles de la bascule sont rédigées au lot 8 (L8.3).
> Voir la section « Nouvelle stack » plus bas.

### Quand créer une entrée

- ✅ Nouvelle feature (UI, API, devops visible côté joueur)
- ✅ Bug fix impactant le comportement joueur
- ✅ Amélioration perf ou UX perceptible
- ❌ Refacto interne sans impact externe
- ❌ Tests, lint, formatage
- ❌ Mise à jour deps sans changement fonctionnel

En cas de doute : créer l'entrée. Le filtre éditorial se fera à la release.

### Où et comment

Un fichier par changement notable : `docs/changelog/YYYY/YYYY-MM-DD-slug-court.md`
(slug kebab-case ; date = livraison, pas début des travaux).
Format : voir `docs/changelog/TEMPLATE.md` — frontmatter YAML strict
(`date`, `type`, `scope`, `title`, `summary`, `tags`), scope ∈ front | back | fetcher | infra | full-stack.
Corps orienté joueur ; contexte technique en fin sous `## Technique`.

### Workflow

1. Implémenter la feature / le fix.
2. **Avant de commit**, créer le fichier `docs/changelog/YYYY/YYYY-MM-DD-slug.md`.
3. L'inclure dans le commit principal (un seul commit avec code + changelog).
4. Plusieurs changements distincts dans la même session → plusieurs fichiers changelog.

Toute génération de commit ou de PR doit vérifier la présence de l'entrée correspondante.
La synthèse vers un changelog public est manuelle, avant chaque release : trier, agréger,
publier, puis **archiver** les entrées traitées dans `docs/changelog/archived/YYYY/`
(cf. `docs/changelog/README.md`).

## Stack

| Couche | Techno |
|---|---|
| Backend | Symfony 7.4 LTS / PHP 8.5 (`app/`) |
| Fetch upstream | micro-service Go `go/fetcher/` (passerelle thin, allowlist SSRF) |
| Stockage assets | Volume Docker `storage` (`/srv/storage`), content-addressed — Flysystem local + écritures atomiques |
| Données utilisateur | PostgreSQL 17 + Doctrine ORM (comptes, favoris, builds) |
| Front | Twig + îlots Vite / Vue 3 / TS / PrimeVue, navigation Turbo Drive |
| Design system | « Hextech » dans `app/assets/styles/app.css` |
| i18n | 21 locales, catalogues `messages.<loc>.yaml`, locale UI = langue Data Dragon |

Ce tableau, les invariants et les règles par langage qui suivent décrivent la stack **en
service**. La réécriture .NET 10 + Angular 22, en cours de construction, a sa propre
section en fin de fichier : « Nouvelle stack (réécriture) ».

## Architecture — invariants à respecter

- **Tout l'egress Data Dragon / CommunityDragon passe par le Go gateway** (`GoFetcherClient` → service `go_fetcher.client`). Ne jamais fetch une URL externe directement depuis PHP. Toute nouvelle source d'asset doit être ajoutée à l'`ALLOWED_HOSTS` du go-fetcher (`compose.yaml`) — **recréer le conteneur** pour prise en compte.
- **Stockage sans base de données** : volume Docker `storage` monté en `/srv/storage` (`STORAGE_DIR`), écrit par php seul, **lecture seule** pour nginx et go-api. Toute écriture passe par `AtomicWriteAdapter` (fichier `.staging/` + `rename()`) — ne jamais le contourner ni écrire en direct : nginx, go-api et le read-merge-write liraient un fichier à moitié écrit (et un blob tronqué serait mis en cache `immutable` un an). nginx n'expose que `blobs/` (`/cdn/blobs/`, allow-list) : `data/`, `manifest/`, `analytics/`, `audit/` ne sont jamais servis. Images en `blobs/{sha256}.{ext}` (dédup O(1) + sibling WebP), données en `data/{version}/{lang}/{type}.json`, manifeste `manifest/{version}/{type}.json`. Le manifeste se met à jour en **read-merge-write** (`AbstractManager::saveManifest`) — ne jamais réintroduire un overwrite aveugle (course concurrente loader SSE ↔ flush kernel.terminate). Une entrée manifeste à **`null` = absence définitive persistée** (403/404 CDN, ex. icônes `.dds` des runes 7.22–8.7) : résolue en placeholder sans re-fetch, à distinguer d'une clé absente (jamais tentée).
- **Postgres = données utilisateur uniquement** (comptes/favoris/builds). Les données et images Data Dragon restent hors DB (volume `storage`) — ne jamais y introduire de dépendance DB. Migrations : `docker compose exec -T -u www-data php php bin/console doctrine:migrations:migrate`.
- **Tout état durable écrit sur disque va sous `var/state/`** (volume `app_state`) : events analytics, journal d'audit, sessions, base GeoLite2 — seule exception, le stockage DDragon (volume `storage`, cf. ci-dessus). Un déploiement = `compose pull` + `up -d`, donc **recréation du conteneur** : le reste de `var/` (cache, log) est détruit — et doit le rester, persister `var/cache` servirait un conteneur DI compilé depuis une image antérieure. Un nouveau point de montage doit exister dans l'image et être `chown www-data` **avant** `USER www-data` (Docker recopie les droits du point de montage en peuplant un volume vide ; un dossier absent est créé `root`).
- **Objets et sorts d'invocateur : indexer par id, jamais par nom.** Depuis League of Legends Classic, Data Dragon livre des jumeaux homonymes (`1004` / `771004` « Charme féerique », `SummonerFlash` / `SummonerFlash_Jade`). L'édition est une dimension à part entière (`App\Service\API\Edition` : objet Classic = id `^77\d{4}$`, sort Classic = mode `JADE` — ne pas dériver des flags `maps`, incohérents). Toute map `images`/options/erreurs est indexée par id ; les jumeaux sont cross-linkés via `EditionAwareInterface::counterpart()`. Pas de mode de build Classic (pas de runes réforgées côté DDragon).
- **Managers de ressource** (champion/item/rune/summoner) : dérivent `AbstractManager`. La logique partagée (data, images, manifeste, **pagination**) vit dans la base ; un manager concret ne surcharge que ses points de divergence réels (ex. `paginationCollection`, `perPageCap`, `imageUrl`, `imageEntries`).
- **Contrôleurs de ressource** : dérivent `AbstractResourceController` (dépendances transverses + `dataError`/`redirectToSetupWithError`/`clientData` mutualisés). Résolution version/langue via `PageContextResolver` (query → session, **sans redirect**), jamais de « redirect dance ».
- **Îlots Vue** : logique d'orchestration hors du SFC (composables + modules purs, ex. `assets/vue/loader/`). Un SFC reste présentation + câblage mince. Code-split par îlot.

## Conventions de code

Les **limites chiffrées** sont des plafonds à respecter ; les **principes** sont des défauts à suivre sauf raison explicite et justifiée.

### Principes directeurs

- **DRY** — Pas de duplication de logique ni de connaissance métier : une règle vit à un seul endroit. N'abstrais pas avant la 3ᵉ répétition (une duplication ponctuelle vaut mieux qu'une mauvaise abstraction).
- **KISS** — La solution la plus simple qui résout réellement le problème. Pas de complexité gratuite.
- **SOLID** :
  - **S** — Responsabilité unique : une classe/module n'a qu'une seule raison de changer.
  - **O** — Ouvert à l'extension, fermé à la modification.
  - **L** — Un sous-type remplace son parent sans casser le comportement attendu.
  - **I** — Interfaces ciblées plutôt qu'une interface fourre-tout.
  - **D** — Dépends d'abstractions, pas d'implémentations concrètes.
- **CQS** — Une fonction *modifie* l'état OU *retourne* une valeur, jamais les deux.

### Limites de taille et de complexité

| Règle | Limite |
|---|---|
| Taille d'un fichier | ≤ 300 lignes (alerte), 400 maximum |
| Fichiers par dossier | ≤ 10 (au-delà, découper en sous-dossiers par domaine) |
| Taille d'une fonction / méthode | ≤ 30 lignes |
| Paramètres d'une méthode | ≤ 3 (au-delà, regrouper dans un objet/DTO) |
| Profondeur d'imbrication | ≤ 3 niveaux |
| Complexité cyclomatique | ≤ 10 par fonction |
| Longueur de ligne | ≤ 100 caractères |

> Seuils alignés sur les conventions B-Hive. Le skill `archi-report` signale encore
> ⚠️ 300 / 🔴 500 : le plafond qui fait foi ici est **400**.
> **Exception paramètres** : les constructeurs à injection de dépendances Symfony sont exemptés de la règle des ≤ 3 (le conteneur assemble) ; regrouper seulement si la liste devient vraiment illisible.

- **Un seul élément public par fichier**, nommé comme le fichier (classe PHP, composant Vue, module TS).
- **Pas de nombres ni de chaînes magiques** — constantes nommées explicitant l'intention (ex. `INGEST_CHUNK_SIZE`, `WATCHDOG_IDLE`).

### Nommage

- Noms explicites révélant l'**intention** (le *quoi*/*pourquoi*, pas le *comment*).
- Casse idiomatique cohérente et jamais mélangée : PHP/TS → `PascalCase` (types/classes/composants), `camelCase` (variables/méthodes) ; constantes `UPPER_SNAKE`.
- Pas d'abréviations cryptiques (`userCount`, pas `usrCnt`) ; seules `id`, `url`, `http`, `sha`… tolérées.
- Booléens préfixés `is`/`has`/`should`/`can` (`isActive`, `shouldDefer`).

### Fonctions

- **Une fonction = une seule chose** — si tu dois écrire « et » pour la décrire, découpe-la.
- **Privilégie les fonctions pures** ; rends les effets de bord explicites.
- **Guard clauses / return early** sur les cas limites plutôt que d'imbriquer des `if/else`.
- **Évite les flag parameters** qui changent le comportement (deux fonctions déguisées) — sépare-les. (Toléré uniquement pour un opt-in orthogonal documenté, ex. `allowDefer`.)

## Règles par langage

### PHP (Symfony)

- **`declare(strict_types=1);`** en tête de **chaque** fichier. Classes **`final`** par défaut (sauf base abstraite conçue pour l'extension).
- Typage strict partout (propriétés, params, retours), `readonly` pour l'injection. Enums/DTO plutôt que tableaux associatifs quand la forme est stable.
- Autowiring : déclaration explicite dans `services.yaml` uniquement pour les scalaires/bindings non déductibles. Pas de service mort (une classe non injectée et sans usage = à supprimer, pas à garder « au cas où »).
- Erreurs : exceptions typées ; l'absence définitive upstream (403/404) n'est pas une erreur (fallback/vide persisté), les erreurs transitoires (5xx/timeout) remontent.

### TypeScript / Vue

- `<script setup lang="ts">` + typage strict (`vue-tsc --noEmit` doit passer). Pas de `any` implicite.
- SFC = présentation ; extraire l'orchestration en composables (`useXxx`) et helpers purs (testables sans monter le composant).
- Réutiliser le design system (`app.css`, variables `--color-*`, `--font-*`, `--ease-hextech`) — ne pas redéclarer de couleurs/typo en dur.

### Go

- Tester en conteneur (`golang:1.26`) — Go non installé en local. Passerelle thin : pas d'ingestion métier côté Go.

## Commentaires

- **En anglais** côté code. Expliquent le **pourquoi** (décision, trade-off, piège), pas le *quoi* que le code dit déjà.
- Pas de commentaires « tutorial-style » ni de docblocks qui paraphrasent la signature. Un docblock a de la valeur s'il documente un contrat, un invariant, ou une divergence volontaire.

## Garde-fous (à lancer avant de considérer un lot terminé)

```bash
# Backend (dans le conteneur, comme la CI) — TOUJOURS `-u www-data`, voir Pièges connus
docker compose exec -T -u www-data php php vendor/bin/phpunit tests/Unit   # baseline verte
# Front (hôte, depuis app/)
npm test          # vitest
npm run typecheck # vue-tsc --noEmit
npm run build     # vite build
```

- Stack : `docker compose up -d --build` → app `:8080`, Mailpit `:8025`, go-fetcher `:8085/healthz`. Docker CLI uniquement via l'outil **Bash** (pas PowerShell).
- ⚠️ `tests/Functional/AdminAccessTest` échoue en conteneur `APP_ENV=dev` (`framework.test` inactif) — **pré-existant**, verte en CI. Garde-fou backend = `tests/Unit`.
- **Toujours benchmarker la perf en prod** : les ~5 s perçus en dev = overhead profiler/`debug=true`, pas la couche data (managers ~0 ms à chaud).

## Pièges connus (ne pas « corriger » par erreur)

- **Loader** : ne PAS réintroduire `<Transition>` + `v-show` pour l'overlay — la transition de sortie ne se complète pas de façon fiable en vrai navigateur. Visibilité = toggle de classe CSS déterministe.
- **Splash / skins champion** : servis **directement** depuis le CDN DDragon (hotlink assumé), PAS ingérés dans le stockage — choix perf sur le TTFB.
- **Chromas** : seule source = CommunityDragon (booléen `chromas` seul côté DDragon). Label couleur dérivé de la teinte (honnête), pas un nom produit Riot.
- **`/build/` est réservé aux assets Vite (nginx)** — la vue de partage des builds vit sur `/b/{token}` ; ne pas « corriger ».
- **CSRF stateless** : token ids `submit`/`authenticate`/`logout` — les POST curl de test exigent un header `Origin`.
- **`docker compose exec php …` tourne en root** (`user: root` en dev, cf. `compose.override.yaml`) : toute commande console/phpunit lancée sans `-u www-data` laisse des dossiers root sous `var/` que le pool FPM (www-data) ne peut plus écrire. Symptôme : `RuntimeException: Unable to create the storage directory (var/cache/dev/profiler/…)` levée **après** l'envoi des headers → page d'erreur 500 collée en fin de HTML sur toutes les pages dev, `_wdt` en 404. Correctif au démarrage : `chown -R` sur `var` et `/srv/storage`. **Toujours `docker compose exec -u www-data php …`.**
- **Préservation du comportement** en refacto : conserver les contrats publics et la sortie ; prouver l'équivalence (tests + diff de rendu avant/après) avant de commit.

## Références

- `docs/architecture/architecture-report.md` — état archi + refactos appliqués (DRY/SOLID/KISS).
- `docs/architecture/architecture.md`, `docs/guides/docker.md`, `docs/guides/configuration.md`, `docs/audits/performance-audit.md`.

## commit

Convention de commits (maintenue par /commit, initialisée par /b-hive-init).
- Style : Conventional Commits — langue : fr (sujets sans accents, impératif, ≤ 80 car.)
- Scopes (chemin → scope) :
  - `app/src/Service/Seo/**`, `app/src/Twig/SeoExtension.php`, `app/public/robots.txt` → `back/seo`
  - `app/src/EventSubscriber/LocaleSubscriber.php`, `app/src/Service/I18n/**` → `back/i18n`
  - `app/src/Controller/Admin/**`, `app/src/Service/Admin/**`, `app/templates/admin/**`, `app/public/admin/**` → `back/admin`
  - `app/src/Service/API/**` → `back/api`
  - `app/src/Service/Analytics/**` → `back/analytics`
  - `app/src/Security/**` → `back/security`
  - `app/src/Command/**` → `back/console`
  - `app/migrations/**`, `app/src/Entity/**`, `app/src/Repository/**` → `back/db`
  - `app/config/**` → `back/config`
  - `app/src/Controller/**` (hors Admin) → `back/<domaine>`
  - `app/templates/**`, `app/assets/**` → `front/<zone>`
  - `app/translations/**` → `i18n`
  - `src/LoDb.Web/src/app/features/editorial/changelog/published/**` (+ archivage
    `docs/changelog/**` de la même release) → `changelog`
  - Lot touchant à la fois contrôleur + templates + traductions → `full-stack/<zone>`
  - `go/fetcher/**` → `fetcher`
  - `go/api/**` → `api`
  - `compose*`, `docker/**`, `infra/**` → `infra`
  - `tools/**` → `tools`
  - `screenshot/**` → `docs/screenshots`
  - `.github/**` → `ci`
  - `docs/**` → `docs`
  - config transverse racine (`tailwind.config.js`, `composer.json`, `package.json`, lockfiles) → `chore` (sans scope)
- Règles de regroupement :
  - les tests (`app/tests/**`) voyagent avec le code testé, jamais en commit séparé ;
  - les entrées `docs/changelog/**` sont jointes au commit feature/fix qu'elles documentent.

## Tests

- **Toute feature s'accompagne de tests**, livrés avec elle (même branche, même commit/PR que le code testé).
- **Uniquement le nécessaire pour tester la feature** : le comportement nominal et les cas limites qu'elle introduit. Pas de course au pourcentage de couverture, pas de tests redondants ; on ne teste ni le framework ni les bibliothèques tierces.
- Un bon test échoue quand le **comportement** de la feature casse — pas quand son implémentation change.
- Emplacements : PHPUnit dans `app/tests/Unit` (baseline verte, cf. Garde-fous) et `app/tests/Functional` ; Vitest dans `app/tests/js`.

## Nouvelle stack (réécriture)

Réécriture en .NET 10 + Angular 22, construite **à côté** de la stack en service, sur la
branche d'intégration `docs/reecriture-dotnet-angular`, jusqu'à la bascule (lot 8). Le
fond fait foi dans [`docs/reecriture/`](docs/reecriture/README.md) (`heritage.md`, `adr/`) ;
le [plan](docs/reecriture/plan-implementation.md) fixe chantiers, périmètres, fichiers
partagés (§7.3) et jalons (§8). Commandes : [`dev-next.md`](docs/guides/dev-next.md).

- **L'ancienne stack est en lecture seule** : `app/`, `go/`, `docker/nginx/`,
  `docker/php/`, `compose.yaml`, `compose.override.yaml`, `compose.deploy.yaml` et les
  workflows existants. Elle sert de référence de comportement ; ses règles (sections
  ci-dessus) restent valables pour elle seule.
- Les règles de code communes (limites, nommage, fonctions, un élément public par
  fichier, commentaires en anglais) s'appliquent au nouveau code ; pas les garde-fous PHP.

### Arborescence

```
LoDb.slnx  global.json  Directory.Build.props  Directory.Packages.props  .editorconfig
compose.next.yaml  compose.next.override.yaml (dev)  compose.next.deploy.yaml  .env.next.example
docker/next/{api,web-ssr,nginx}/     images lodb-api, lodb-web-ssr, lodb-nginx
  nginx/sites/ (site, sous-domaine api.)  server.d/ (inclus dans le server du site)  snippets/
src/LoDb.Domain/          pur, sans I/O : versions, langues, éditions, chemins canoniques
src/LoDb.Ingestion/       Egress/ Ddragon/ Pipeline/ Catalog/ …
src/LoDb.Infrastructure/  Persistence/ Storage/ Jobs/ Outbox/ Audit/ Analytics/ DataProtection/
src/LoDb.Api/             Program.cs Hosting/ Cli/<Zone>/ Workers/<Zone>/ Modules/<M>/ openapi/
src/LoDb.Web/             workspace Angular : src/app/{core,ui,features}  src/server/  public/i18n/
tests/LoDb.*.Tests/  tests/LoDb.Testing/ (ApiFactory, PostgresContainerFixture)  tests/LoDb.E2E/
tools/next/               scripts (contrat, i18n, parité, bascule)
docs/reecriture/rapports/ rapports des chantiers et des jalons
```

Références : `Api` → `Ingestion` → `Infrastructure` → `Domain`. Les 14 modules
(`Add<Module>`/`Map<Module>`) et les 11 zones (`AddLoDb<Zone>`) sont pré-enregistrés :
**`Program.cs` ne change plus**, chaque chantier ne remplit que son fichier.

### Invariants (ADR)

- **Un seul hôte `LoDb.Api`** (ADR 0002) sert `/api`, `/v1`, `/webhooks` et les tâches de
  fond. L'egress Data Dragon et CommunityDragon passe par le client nommé `ddragon` et son
  allow-list (https, deux hôtes, chaque redirection re-vérifiée, plafond de taille).
  403/404 = **absence définitive**, rendue comme une valeur ; 5xx/timeout = transitoire,
  jamais persisté.
- **`Add*` et `Map*` ne font aucune I/O et ne lèvent rien** : la génération OpenAPI
  exécute `Program` sans configuration. Sous-commandes : `ICliCommand` sous
  `Cli/<Zone>/` ; tâches de fond : `BackgroundService` sous `Workers/<Zone>/`. Toutes deux
  sont découvertes par convention. `LoDb:Workers:Enabled=false` coupe toutes les tâches.
- **Tâches périodiques** (ADR 0003) : `PeriodicTimer` + verrou consultatif Postgres, files
  `Channel<T>` bornées, **une ligne de synthèse par lot**, jamais une par URL.
- **Stockage** (ADR 0004) : blobs derrière `IBlobStore`, adressés par contenu, écrits de
  façon **atomique** (temporaire sur le même volume, puis `File.Move`) ; nginx ne sert que
  `/cdn/blobs/`. Manifeste, analytics, audit et clés Data Protection en Postgres (`absent`
  = absence définitive). Rien qui empêche N instances, **aucune session serveur**.
- **Objets et sorts indexés par id, jamais par nom** (jumeaux Classic), comme aujourd'hui.
- **SSR** (ADR 0005) : le serveur ne lit **aucun cookie** et ne détient **aucun secret**.
  La locale est en préfixe d'URL (`/{locale}/…`, 21 locales). L'id fait foi, le slug est
  décoratif (301 vers la canonique). Le thème passe par un script inline, et `www.` et
  `.fr` sont redirigés par nginx.
- **Front** (ADR 0006) : il ne consomme que le **client généré** depuis OpenAPI, sans
  copie manuelle des contrats (locales, motif de version, types). Aucun
  `if (platform)` hors de `core/platform/`, aucun kit UI, pas de NgRx.
- **Persistance** : une seule migration EF par lot, écrite par le chantier de schéma
  (L1.4, L4.1, L6.2), seul à toucher entités et configurations. Migrations **additives**
  jusqu'à la phase *contract*, car l'ancienne stack tourne sur le même schéma.
- **Identité** (ADR 0009) : hash argon2id PHC, lisibles par `password_verify` de PHP.
- **Observabilité** (ADR 0010) : logs JSON d'**une ligne par enregistrement** avec
  `trace_id`, `EventName` en `domaine.sujet.resultat`, **aucune clé de contexte `error`**,
  aucune donnée personnelle. `/metrics` est servi sur 9464, **jamais routé ni publié**.
  `/healthz` ne dépend de rien ; `/readyz` vérifie Postgres et le stockage.
- **Apps** (ADR 0007, 0008) : Photino derrière `IDesktopShell`, versions épinglées ;
  Capacitor 8 et live update signé ; `X-LoDb-Client` et `426` : l'API décide.
- **Artefacts générés** (`openapi/*.json`, `core/api/generated/`, `package-lock.json`) :
  jamais édités à la main ni committés par un chantier ; l'intégration les régénère.

### Conventions (plan §4)

**C#.** `net10.0`, `Nullable`, `TreatWarningsAsErrors`, `AnalysisLevel`
`latest-recommended`, espaces de noms au niveau du fichier (`LoDb.<Projet>.<Dossier>`).

- Classes `sealed` par défaut, `record` pour les DTO et les objets valeur, membres
  `required`, constructeurs primaires pour l'injection.
- Aucun état statique mutable : `TimeProvider` injecté, jamais `DateTime.UtcNow` ;
  `CancellationToken` sur toute méthode asynchrone.
- `[LoggerMessage]`, exception passée en objet ; exceptions typées pour le transitoire,
  l'absence définitive est une valeur.
- `/api` en ProblemDetails, `/v1` garde `{error:{code,message}}` et le snake_case de go-api.
- Minimal APIs (`MapGroup` par module, `TypedResults`) ; options `LoDb` validées au
  démarrage ; versions NuGet dans `Directory.Packages.props` seul, jamais flottantes.

**TypeScript / Angular.** Composants standalone, signals, zoneless, `OnPush`,
`inject()`, `input()`/`output()`, blocs `@if`/`@for`/`@defer`. TypeScript et templates
stricts, pas de `any`.

- Un composant = présentation + câblage mince ; l'orchestration vit dans des services et
  des fonctions pures.
- ESLint bloquant : `@capacitor/*` et `@capawesome/*` seulement sous `core/platform/` ;
  une feature n'importe que `core/`, `ui/` et elle-même.
- Styles : jetons Hextech, rien en dur, propriétés logiques (RTL), champs de saisie à
  16 px au moins.
- Tests Vitest en `*.spec.ts`, à côté du code. Une feature met ses E2E dans
  `tests/LoDb.E2E/specs/<feature>/`, ses nouvelles clés i18n dans `public/i18n/<feature>/`.

**Commits.** Conventional Commits en français, sujet à l'infinitif et sans accents,
**72 caractères au plus**. Les tests vont dans le même commit, sans aucune ligne
d'attribution. `git add` se fait par chemins explicites. Carte des scopes :

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

Les autres chemins (`.github/**`, `docs/**`…) gardent la carte de la section « commit ».

### Garde-fous (plan §6.2)

```bash
dotnet build LoDb.slnx -c Release            # 0 avertissement
dotnet test LoDb.slnx                        # Testcontainers : Docker démarré
npm ci --prefix src/LoDb.Web
npm --prefix src/LoDb.Web run lint           # puis typecheck, test, build:web, build:shell
npm --prefix src/LoDb.Web run api:generate   # contrat (L2.2 ; bouchon avant)
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build
npm --prefix tests/LoDb.E2E test             # stack démarrée (LODB_E2E_BASE_URL sinon)
npm --prefix src/LoDb.Web run api:check      # dérive du contrat (job contract de next-ci)
bash tools/next/routing/check-urls.sh        # grammaire d'URL de l'ADR 0005, stack démarrée
node tools/next/accounts/legacy-logins.mjs   # critère du lot 4 (hash hérités), stack démarrée
node tools/next/seo-diff/diff.mjs --stack lodb-next   # critère du lot 3, prod en GET seul
node tools/next/lighthouse/run.mjs --stack lodb-next  # budgets du lot 3, stack démarrée
```

- **Stack d'intégration `lodb-next`**, une seule instance, depuis la racine : 18080 nginx,
  18081 API, 18082 SSR, 15432 Postgres, 18025 Mailpit. Emplacements `lodb-next-e1` et
  `-e2` (181xx/182xx), lancés depuis un worktree et supprimés (`down -v`) avant de rendre.
- **Ne jamais toucher** aux conteneurs `chewb-*`, `laforce-*` et `grafana/mcp-grafana`,
  ni à leurs ports (3307, 33306, 21200 à 21213). Le port 9464 n'est jamais publié.
- Métriques : lues depuis le réseau de la stack (`wget http://api:9464/metrics` dans le
  conteneur nginx, commande complète dans `dev-next.md`).
- Validation des workflows : image `rhysd/actionlint` sur `.github/workflows/next-*.yml`.
- Chaque jalon consigne son rapport dans `docs/reecriture/rapports/jalons/lot-NN.md`, et
  les pics mémoire des E2E dans `docs/reecriture/rapports/memoire.md`.

### Pièges du lot 0 (ne pas « corriger » par erreur)

- `dotnet test` compile en **Debug**, le build du plan en **Release** : les deux coexistent.
  `bin/` et `obj/` ne sont ignorés que sous `/src` et `/tests` (`app/bin/` est suivi).
- Documents OpenAPI : `dotnet build src/LoDb.Api -p:LoDbGenerateOpenApi=true` écrit
  `openapi/LoDb.Api_app.json` et `LoDb.Api_public-v1.json`. Ces noms sont imposés par le
  SDK. `MapOpenApi` n'est actif qu'en Development.
- **npm 12** bloque les scripts d'installation (`allowScripts`) d'`esbuild`, `lmdb`,
  `@parcel/watcher` et `msgpackr-extract`. Les binaires viennent des paquets optionnels de
  plateforme. Si l'image `web-ssr` échoue au build, c'est la première piste.
- **`autoCsp` est incompatible avec le SSR** en Angular 22 : retiré, nonce en L3.11.
  `withFetch` et `withIncrementalHydration` sont omis (dépréciés, défaut en v22).
- Variables Compose préfixées **`LODB_`** : Compose lit aussi le `.env` racine de
  l'ancienne stack. En dev, les images s'appellent `<projet>-<service>` : un emplacement
  n'écrase donc pas les images de l'intégration.
- nginx : sous-domaine reconnu par `server_name ~^api\.` (`API_CADDY_DOMAINS` commence
  par `api.`) ; CORP `same-origin` sauf `/cdn/blobs/` en `cross-origin` (coquille
  Android) ; `access_log off` voulu (l'edge journalise) ; `sites/*.conf` passe par
  `envsubst`, filtré sur `^LODB_`.
- SSR : `LODB_TRUST_PROXY_HEADERS` obligatoire derrière nginx (sinon `X-Forwarded-*`
  ignorés), `LODB_ALLOWED_HOSTS` liste les hôtes acceptés, `/` renvoie un 302 vers `/en/`
  jusqu'à L3.1 ; `public/` ne contient ni `media/` ni script racine nommé comme un bundle
  (cache `immutable`).
- `/readyz` se lit sur l'API (18081 ou `api:8080`) ; demandé à nginx, il part au SSR (404).
- Constats du jalon 0, **à corriger à la source, jamais à filtrer**
  ([rapport](docs/reecriture/rapports/jalons/lot-00.md)) :
  - Npgsql écrit deux lignes natives `libgssapi_krb5.so.2` hors JSON au premier accès à
    Postgres (image *chiseled*) ;
  - le SSR écrit en multi-ligne les erreurs de rendu (`console.error` de l'`ErrorHandler`
    d'Angular).
- L'avertissement « clés Data Protection éphémères » de l'API est attendu jusqu'à L4.1.
- `src/LoDb.Web/` compte déjà 11 fichiers à sa racine, `package-lock.json` compris : la
  limite de 10 par dossier y est à trancher avant L3.2 (`.postcssrc.json`).
- L'ancienne stack (projet `lodb`) peut tourner depuis le checkout principal : on ne la
  recrée jamais depuis un autre dossier (label `com.docker.compose.project.working_dir`).
  Ses commandes console gardent `-u www-data`.

### Pièges du lot 1 (ne pas « corriger » par erreur)

- Parité (L1.8) : `tools/next/parity/` collecte, `tests/LoDb.Parity` compare. Un nouvel
  écart reçoit une règle de `ParityRules.cs`, argumentée dans
  `docs/reecriture/rapports/parite-lot-1.md`, jamais un filtre dans la comparaison. Sans
  `LODB_PARITY_RUN`, les 2 tests du run sont ignorés.
- L'ancienne stack ne stocke détails, icônes de sorts et chromas qu'à la visite d'une page
  détail, et son warmup ne prend que les images listées en première langue. Une image
  `pending` de son côté n'est donc pas un écart.
- `app:ddragon:warmup` sur l'échantillon de parité dépasse 256 Mo en dev : lancer
  `php -d memory_limit=2G bin/console …`, toujours en `-u www-data`.
- `ingest` sort en 1 si une image reste sans verdict (503 transitoire, rien de persisté) :
  relancer `ingest --version` de la version citée. Un `ingest --languages …` n'écrit pas
  `ddragon_version` : seule une ingestion toutes langues fait avancer l'état.
- 7050 « Gangplank Placeholder » est traduit en `ar_AE` et `zh_CN`. Il y reste listé dans
  les deux stacks : c'est un défaut commun, G1 du
  [jalon 1](docs/reecriture/rapports/jalons/lot-01.md).

### Pièges des fondations du lot 3 et du lot 4 (ne pas « corriger » par erreur)

- **Issues de navigation rendues en place** (L3.1) : 301, 302, 404 et 5xx s'affichent à
  l'URL demandée (`PageResponse` écrit statut, `Location` et cache dans `RESPONSE_INIT`).
  Jamais d'`UrlTree` de redirection côté serveur. `/{locale}/account`, `/{locale}/u` et
  `/b` ont une route 404 explicite (`bareNotFound`) : le routeur accepte sinon un montage
  paresseux vide, qui répond 200. À retirer seulement si une page prend ce chemin.
- Les routes en `RenderMode.Client` (compte, `/admin`) ne peuvent pas poser de statut :
  un chemin de compte inconnu répond 200, puis affiche la 404 dans le navigateur.
- Les pages lisent leurs données par `injectRouteData(clé)`. La liaison des entrées de
  composant par le routeur reste coupée : une query forgée remplirait des entrées.
- `app.routes.server.spec.ts` passe par `ɵextractRoutesAndCreateRouteTree`, une entrée
  privée d'Angular : une montée de version peut la déplacer.
- Points d'accroche de `app.config.ts` : chaque propriétaire remplit son dossier
  (`core/auth`, `core/analytics`, `core/update`), et les composants provisoires des
  emplacements, sans toucher `app.config.ts` ni `app.html`.
- **SEO** : une page ne touche jamais au `<head>`. Elle appelle
  `inject(Seo).apply(SeoPage)`, et le service ajoute le `siteGraph`. Une page qui oublie
  cet appel n'a ni canonique ni hreflang : c'est G2 du
  [jalon 2](docs/reecriture/rapports/jalons/lot-02-fondations-03.md).
- **Scopes i18n** : une clé `about.*`, `api.*` ou `seo.*` n'est traduite que si le
  composant, ou l'un de ses ancêtres, fournit son scope (`provideTranslocoScope`). Sinon
  la clé brute sort dans le HTML, sans erreur (G3 du même jalon). La galerie a ses propres
  chaînes : elle ne prouve rien.
- **E-mails** : le modèle d'un `EmailMessage` s'écrit avec les constantes
  `EmailModelKeys`, jamais en littéral. Un modèle incomplet part en `dead` dès le premier
  essai (`model.invalid`, log `outbox.message.dead`). Les liens que l'API envoie vers le
  front suivent la grammaire de L3.1 (`/{locale}/account/…`).
- L'outbox n'envoie rien sans `LoDb:Mail:Host` : c'est voulu (tests, `api:generate`).
  La surcouche locale vise `mailpit:1025`, et les e-mails se lisent sur
  http://localhost:18025.
- Autorisation (L4.2) : jamais `AllowAnonymous`, car les politiques portent le garde
  XSRF et `Origin`. Bannir, c'est `IsBanned` plus `UpdateSecurityStampAsync`.
- nginx (`server.d/legacy-redirects.conf`) envoie à `/api/legacy` les premiers segments
  de l'ancien site. Tout nouveau chemin hors locale doit être vérifié contre cette liste.
  `server.d/seo.conf` possède `/sitemap.xml`, `/sitemaps/`, `/robots.txt` et
  `/llms.txt`.
- En local, les URLs absolues perdent le port (`http://localhost/…`), car nginx transmet
  `Host: $host`. Ce n'est pas un défaut de l'API : l'origine canonique se fixe par
  `LoDb__Seo__CanonicalOrigin` et `LoDb__Accounts__SiteOrigin` (L8.1).
- `disclosure.spec.ts` échoue parfois en suite complète (`parameter 1 is not of type
  'Event'`), jamais seul : c'est G4 du jalon 2. Relancer n'est pas une correction.

### Pièges du lot 4 et de la vague D (ne pas « corriger » par erreur)

- **Critère du lot 4** : `node tools/next/accounts/legacy-logins.mjs`, stack démarrée.
  Un hash argon2id **plus fort** que la cible (m ≥ 19456, t ≥ 2, p = 1) est conservé, pas
  réécrit : c'est voulu (`Argon2Passwords.IsTarget`). Un compte hérité s'insère sans les
  colonnes Identity (`security_stamp` `NULL`) ; la connexion doit les remplir.
- **Dialogues CDK** : le focus initial est sur `cdk-dialog-container`
  (`autoFocus: 'dialog'`), ancêtre du composant ouvert. Un `host: {'(keydown)'}` n'y
  entend donc rien : écouter `DialogRef.keydownEvents`. Un test unitaire qui émet la
  touche sur l'hôte ne prouve rien (G2 du [jalon 4](docs/reecriture/rapports/jalons/lot-04.md)).
  Un dialogue plus haut que l'écran doit rester défilable jusqu'à son pied (G1).
- **Liens du catalogue** : jamais une chaîne avec `?` ou `#` passée à `[routerLink]`, qui
  les encode (`%3F`, 404). Passer une `UrlTree` (`injectCatalogueLink`) ; G3 du jalon 4.
- **Specs E2E** : comparer le chemin d'une réponse (`new URL(url).pathname`), jamais
  `url.endsWith(...)`, que la query `?version=` fait échouer. Limiter les comptes de titres
  ou de boutons à `main` ou au composant visé : le pied de page a ses `h2`, l'éditeur de
  profil son propre bouton « Sign out ». Un parcours qui crée un compte le supprime
  aussi en cas d'échec.
- **Variante store** (L10.1) : `environment.payments` n'a d'effet que là où le code le
  lit. esbuild émet le chunk des dons même quand `payments` vaut `false` : on retire le
  paiement par le routage et les liens. `cap sync` ne tourne que dans le conteneur
  (`tools/next/android/build-debug.sh`) ; sur l'hôte, il copie le bundle dans
  `android/…/assets/public`, et `prettier --check .` échoue.
- `npm install --prefix src/LoDb.Web` tire `@capacitor/ios` et `@capacitor/keyboard` par
  `@aparajita/capacitor-secure-storage` : c'est attendu, il n'y a pas de plateforme iOS.
- **Desktop** : `dotnet test` compte `LoDb.Desktop.Tests`, qui n'ouvre aucune fenêtre ;
  `--smoke` ne sort pas du poste. Photino n'est référencé que par
  `Shell/Photino/PhotinoShell.cs`, et un test le vérifie.

### Pièges du jalon 3 (ne pas « corriger » par erreur)

Constats du [jalon 3](docs/reecriture/rapports/jalons/lot-03.md).

- **Quota d'inscriptions** : 5 par heure et par adresse (`RateLimitingPolicies`), et
  toute la suite E2E arrive par nginx sous une seule adresse. Le limiteur vit en mémoire :
  entre deux passages complets, lancer `docker restart lodb-next-api-1`. Seule
  `specs/account/register.spec.ts` passe par le formulaire d'inscription ; toute autre
  spec qui a besoin d'un compte le crée par la CLI (`createMember` et `signInMember` de
  `support/member-account.ts`, ou le compte de son worker dans `support/worker-account.ts`).
  Ne jamais relever le quota pour faire passer les tests (G5, puis G3 du lot 8).
- **Sonde de 320 px** (`specs/public/layout.spec.ts`) : l'en-tête n'a que 288 px utiles.
  Tout ajout au groupe d'actions (dons, compte, thème, sélecteur de contexte) se vérifie
  à 320 px, en `ar` compris. Un `white-space: nowrap` sur un titre doit pouvoir passer à la
  ligne sur téléphone (G1, G2).
- **Balise d'analytics** : chaque navigation interne envoie `POST /api/analytics/view`
  (L7.1). Une spec qui compte les POST exclut `/api/analytics/` (G3).
- **Compression** : le SSR sert les bundles en `text/javascript`. `gzip_types` de
  `docker/next/nginx/nginx.conf` doit lister ce type (et `application/manifest+json`),
  sinon Lighthouse perd environ 1,7 s en 4G lente (G4). Vérifier par `curl -D - -H
  'Accept-Encoding: gzip'` sur un `/build/*.js`. Un budget Lighthouse ne se relâche jamais.
- **Rapports générés** : `diff-seo.md` et `lighthouse.md` ne s'écrivent que par leurs
  outils. Un écart SEO nouveau reçoit une règle argumentée dans
  `tools/next/seo-diff/lib/rules.mjs`, et on relance l'outil. Ne jamais passer `--json`
  ni `--out` vers `/tmp` pour une exécution committée : la commande est écrite dans le
  rapport.
- **Sélecteurs de build** : `/api/pickers/*` exige `version` et `lang` (400
  `invalid-version` sinon). Aucune session ne les fournit.
- `api` dépasse 384m pendant la suite E2E complète (541 Mio en dev, sans limite) : ce
  n'est pas une fuite prouvée. On le mesure sous limite avant L8.1, sans rien forcer
  dans le code.

### Pièges du jalon des lots 5 à 7 (ne pas « corriger » par erreur)

Constats du [jalon des lots 5 à 7](docs/reecriture/rapports/jalons/lots-05-06-07.md).

- **CLI de l'hôte en `Development`** : l'API de la stack de dev tourne en `Development`, où
  l'hôte de `CliRunner` valide tout le conteneur. Un service de module qui dépend de
  l'hôte web (autorisation, sondes de santé, routage) fait tomber **toutes** les
  sous-commandes (`admin create`, `analytics import`, `ingest`…), sans qu'aucun test ne le
  voie. Après tout ajout à un `Add<Module>`, lancer dans la stack
  `docker compose -p lodb-next … exec -T api dotnet LoDb.Api.dll analytics import --source /x --dry-run`
  (attendu : `No such directory: /x`), et non un contournement par
  `ASPNETCORE_ENVIRONMENT=Production` (G1).
- **Le contrat avant le front** : une branche qui importe un client généré non commité ne
  compile qu'après `api:generate` ; l'intégration le lance et le committe avant les
  suites.
- **Parité des builds** (`tools/next/builds-parity/`) : la copie vit dans le Postgres de
  l'ancienne stack (`createdb` + `pg_dump | pg_restore`), jamais dans sa base `lodb`.
  L'ancienne stack y pointe par `POSTGRES_DB` du `.env` (ignoré, sauvegardé puis remis),
  `lodb-next` par une surcouche compose hors dépôt qui ne change que
  `ConnectionStrings__LoDb` (`Host=host.docker.internal;Port=5432;…`). Copier aussi
  `ddragon_version` et `ddragon_asset` de `lodb-next` (données seules) : la copie migrée
  n'a aucune version prête. Pas de `--json` vers `/tmp` pour un rapport commité.
- **Mêmes agrégats analytics** : `app:analytics:rollup` d'abord côté PHP (une journée non
  consolidée n'est lue que par l'ancienne stack), puis `analytics import` avec le volume
  `lodb_storage` monté en lecture seule. L'ancien panneau n'a pas de plage « tout » (il
  retombe sur 30 jours) : comparer 7, 30 et 90 jours. L'ancienne admin se connecte avec
  `ADMIN_LOGIN`/`ADMIN_PASSWORD` du `.env` et le jeton `_csrf_token=csrf-token` plus un
  en-tête `Origin`.
- **Webhook Stripe en local** : aucun secret en dev (503). Pour un rejeu, un secret de
  test `whsec_…` dans une surcouche hors dépôt, une signature `t=…,v1=HMAC-SHA256(t.payload)`
  datée de moins de 5 minutes et `api_version` identique à celle des tests.
- **Textes rendus** : Angular laisse un espace de tête et de queue dans un texte entre
  balises (constaté sur `/developers`, `" Base URL: … "`) ; une assertion Playwright par
  regex ancrée sur `^` ou `$` échoue alors. Ancrer sur le contenu, ou utiliser une chaîne
  (G3 ; hypothèse aussi pour `/^Relevé du /` de G2).

### Pièges du jalon du lot 8 (ne pas « corriger » par erreur)

Constats du [jalon du lot 8](docs/reecriture/rapports/jalons/lot-08.md).

- **Répétition locale** (critère du lot 8) : `docs/reecriture/bascule.md` § 3.1, tel
  qu'écrit, depuis la racine. Elle occupe l'emplacement `lodb-next-e2`, jamais `lodb-next`,
  et ne tourne ni pendant un build Android ni quand l'emplacement 2 est déjà occupé.
  L'ancienne stack doit être arrêtée, ou lancée depuis ce dossier. Le script vise la copie
  par `POSTGRES_DB` dans l'environnement de `docker compose` : ne jamais modifier le `.env`
  pour elle.
- Le code de `rehearse.sh … | tee …` est celui de `tee` : lire `$pipestatus[1]` (zsh) ou
  `${PIPESTATUS[0]}` (bash), ou juger sur le résumé (15 lignes `ok` et « The rehearsal
  passes »).
- La base de dev de l'ancienne stack n'a aucun compte à mot de passe : `--anonymize` n'en
  connecte aucun, sans le dire. Les comptes existants prouvés sont les 8 comptes semés
  avant `migrate`, un par format de hash.
- Après la répétition, `lodb_rehearsal` ne doit plus exister dans le Postgres de l'ancienne
  stack (`psql -l`). `lodb_j567` est la copie du jalon des lots 5 à 7, à ne pas confondre
  avec celle de la répétition.
- **Rapport de surveillance de l'admin** : un rapport dont les lectures en base ont échoué
  reste 30 s dans le cache hybride, avec des chiffres vides (G1). Une carte absente de la
  vue d'ensemble ou des opérations se cherche d'abord dans
  `docker logs lodb-next-api-1 | grep admin.monitoring.database_unreadable`, jamais par
  un `waitFor` plus long dans la spec.
- Les instantanés Playwright (`tests/LoDb.E2E/test-results/`) sont écrasés par le passage
  suivant, `readonly-e2e.sh` de la répétition compris : lire les échecs de la suite complète
  avant de lancer la répétition.

### Pièges du réalignement du front (ne pas « corriger » par erreur)

Constats du [réalignement](docs/reecriture/rapports/realignement-front/README.md).

- **Grilles de cartes en `div role=list`**, jamais en `ul/li` ni en `<p>` : le texte riche de
  Riot contient des `<li>` nus, que le navigateur prend pour la fin de la cellule. La cellule
  se referme, l'hydratation ne retrouve plus ses nœuds (`__ngContext__` sur null) et le clic
  sur une carte reste sur la liste.
- **Squelette à la hauteur de ce qu'il remplace** : la forme `portrait` pour une grille de
  portraits. Un squelette plus bas effondre la page pendant le chargement, et les halos du fond
  (placés en pourcentage de sa hauteur) la font compter en CLS (1,2 mesuré).
- **Lighthouse en local est pessimiste** : nginx transmet `Host` sans le port, le cache de
  transfert du SSR range ses réponses sous `http://localhost` et le navigateur les demande à
  `:18080`. Tout est redemandé à l'hydratation, ce qui n'arrive pas en production.
- **Contrat sous Windows** : après `api:generate`, normaliser en `\n` les `\r\n` que le checkout
  CRLF laisse dans les descriptions OpenAPI (`normalize-openapi.mjs` du rapport) ; `api:check`
  signale sinon une dérive faite de ces seuls caractères.
- **`dotnet test` ne prend pas `-m`** : la plateforme de test le passe aux applications de test,
  qui n'exécutent alors aucun test (code 5). `--artifacts-path` fait échouer
  `PhotinoIsolationTests`, qui cherche `LoDb.slnx` en remontant depuis sa sortie.
- Relever le budget `initial` de `build:web` est une décision du propriétaire, pas d'un lot.
