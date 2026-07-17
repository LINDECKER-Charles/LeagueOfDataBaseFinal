# LeagueOfDataBase — instructions projet

Encyclopédie League of Legends servie depuis les données **Data Dragon** (+ CommunityDragon pour les chromas). Ce fichier fixe l'architecture et les **règles de code** du dépôt. Il complète les préférences globales `~/.claude/CLAUDE.md` (senior, concis, production-grade) — ne pas les redupliquer.

## 🔁 Règle critique : versionner chaque feature / fix dans `docs/changelog/`

**À chaque feature implémentée ou bug corrigé, créer une entrée dédiée dans `docs/changelog/`.**

Ce répertoire est le **journal technique interne** : source de vérité de tout ce qui a été
touché, jamais filtré. Aucune entrée = changement invisible.

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
| Backend | Symfony 7.4 LTS / PHP 8.4 (`app/`) |
| Fetch upstream | micro-service Go `go-workers/` (passerelle thin, allowlist SSRF) |
| Stockage assets | MinIO S3 content-addressed (Flysystem + async-aws) |
| Données utilisateur | PostgreSQL 17 + Doctrine ORM (comptes, favoris, builds) |
| Front | Twig + îlots Vite / Vue 3 / TS / PrimeVue, navigation Turbo Drive |
| Design system | « Hextech » dans `app/assets/styles/app.css` |
| i18n | 21 locales, catalogues `messages.<loc>.yaml`, locale UI = langue Data Dragon |

## Architecture — invariants à respecter

- **Tout l'egress Data Dragon / CommunityDragon passe par le Go gateway** (`GoFetcherClient` → service `go_fetcher.client`). Ne jamais fetch une URL externe directement depuis PHP. Toute nouvelle source d'asset doit être ajoutée à l'`ALLOWED_HOSTS` du go-fetcher (`compose.yaml`) — **recréer le conteneur** pour prise en compte.
- **Stockage sans base de données** : images en `blobs/{sha256}.{ext}` (dédup O(1) + sibling WebP), données en `data/{version}/{lang}/{type}.json`, manifeste `manifest/{version}/{type}.json`. Le manifeste se met à jour en **read-merge-write** (`AbstractManager::saveManifest`) — ne jamais réintroduire un overwrite aveugle (course concurrente loader SSE ↔ flush kernel.terminate).
- **Postgres = données utilisateur uniquement** (comptes/favoris/builds). Les données et images Data Dragon restent hors DB (MinIO) — ne jamais y introduire de dépendance DB. Migrations : `docker compose exec -T php php bin/console doctrine:migrations:migrate`.
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
| Taille d'un fichier | ≤ 300 lignes (alerte), 500 maximum |
| Taille d'une fonction / méthode | ≤ 30 lignes |
| Paramètres d'une méthode | ≤ 4 (au-delà, regrouper dans un objet/DTO) |
| Profondeur d'imbrication | ≤ 3 niveaux |
| Complexité cyclomatique | ≤ 10 par fonction |
| Longueur de ligne | ≤ 120 caractères |

> Seuils fichiers alignés sur les skills `archi-report` du projet (⚠️ 300 / 🔴 500).
> **Exception paramètres** : les constructeurs à injection de dépendances Symfony sont exemptés de la règle des ≤ 4 (le conteneur assemble) ; regrouper seulement si la liste devient vraiment illisible.

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

- Tester en conteneur (`golang:1.25`) — Go non installé en local. Passerelle thin : pas d'ingestion métier côté Go.

## Commentaires

- **En anglais** côté code. Expliquent le **pourquoi** (décision, trade-off, piège), pas le *quoi* que le code dit déjà.
- Pas de commentaires « tutorial-style » ni de docblocks qui paraphrasent la signature. Un docblock a de la valeur s'il documente un contrat, un invariant, ou une divergence volontaire.

## Garde-fous (à lancer avant de considérer un lot terminé)

```bash
# Backend (dans le conteneur, comme la CI)
docker compose exec -T php php vendor/bin/phpunit tests/Unit   # 51 tests — baseline verte
# Front (hôte, depuis app/)
npm test          # vitest
npm run typecheck # vue-tsc --noEmit
npm run build     # vite build
```

- Stack : `docker compose up -d --build` → app `:8080`, MinIO console `:9001`, Mailpit `:8025`, go-fetcher `:8085/healthz`. Docker CLI uniquement via l'outil **Bash** (pas PowerShell).
- ⚠️ `tests/Functional/AdminAccessTest` échoue en conteneur `APP_ENV=dev` (`framework.test` inactif) — **pré-existant**, verte en CI. Garde-fou backend = `tests/Unit`.
- **Toujours benchmarker la perf en prod** : les ~5 s perçus en dev = overhead profiler/`debug=true`, pas la couche data (managers ~0 ms à chaud).

## Pièges connus (ne pas « corriger » par erreur)

- **Loader** : ne PAS réintroduire `<Transition>` + `v-show` pour l'overlay — la transition de sortie ne se complète pas de façon fiable en vrai navigateur. Visibilité = toggle de classe CSS déterministe.
- **Splash / skins champion** : servis **directement** depuis le CDN DDragon (hotlink assumé), PAS ingérés MinIO — choix perf sur le TTFB.
- **Chromas** : seule source = CommunityDragon (booléen `chromas` seul côté DDragon). Label couleur dérivé de la teinte (honnête), pas un nom produit Riot.
- **`/build/` est réservé aux assets Vite (nginx)** — la vue de partage des builds vit sur `/b/{token}` ; ne pas « corriger ».
- **CSRF stateless** : token ids `submit`/`authenticate`/`logout` — les POST curl de test exigent un header `Origin`.
- **Préservation du comportement** en refacto : conserver les contrats publics et la sortie ; prouver l'équivalence (tests + diff de rendu avant/après) avant de commit.

## Références

- `docs/architecture-report.md` — état archi + refactos appliqués (DRY/SOLID/KISS).
- `docs/architecture.md`, `docs/docker.md`, `docs/configuration.md`, `docs/PERFORMANCE-AUDIT.md`.
