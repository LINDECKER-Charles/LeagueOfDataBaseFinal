# Rapport d'architecture — LeagueOfDataBase

> Généré le 2026-07-17 sur la branche `feat/archi-refacto`.
> Périmètre : dépôt hors `node_modules`, `vendor`, `dist`, `build`, `.git`, `var`.
> Ce rapport documente l'état **AVANT** puis, en fin de document, l'état **APRÈS** refacto.

## 1. Stack & cartographie

- **Backend** : Symfony 7.4 LTS / PHP 8.4, `strict_types` + classes `final` généralisés.
- **Fetch** : micro-service **Go** (`go-workers`) = passerelle thin vers Data Dragon / CommunityDragon (SSRF allowlist, batch parallèle).
- **Stockage** : **MinIO** content-addressed (Flysystem + async-aws S3), clé = sha256 (dédup O(1)) + manifeste par `(version, type)`. Cache read-through `ddragon.cache`.
- **Front** : Twig + îlots **Vite / Vue 3 / TS / PrimeVue**, navigation **Turbo Drive**, design system « Hextech » (`app.css`).
- **Domaine** : 4 ressources DDragon (champion / item / rune / summoner) via managers dérivant `AbstractManager`.

Répartition (fichiers source, hors tests) : ~28 PHP, 8 Vue/TS, 6 Go.

## 2. Taille des fichiers

Seuils : ⚠️ > 300 lignes · 🔴 > 500 lignes.

| Fichier | Lignes | Seuil | Nature |
|---|---:|:---:|---|
| `assets/vue/components/ResourceLoader.vue` | 717 | 🔴 | SFC (script SSE + template + 300 l. CSS) |
| `src/Service/Client/ClientManager.php` | 413 | ⚠️ | Service (4 responsabilités, dont morte) |
| `src/Service/API/AbstractManager.php` | 401 | ⚠️ | Base managers (data + images + manifeste) |
| `src/Service/API/ChampionManager.php` | 312 | ⚠️ | Manager + logique chroma CDragon |
| `assets/vue/components/ChromaStrip.vue` | 251 | | SFC autonome (OK) |
| `src/Controller/HomeController.php` | 213 | | Contrôleur (docblocks verbeux) |
| `src/Service/API/ItemManager.php` | 177 | | Manager |
| `src/Service/Tools/Utils.php` | 171 | | **Fourre-tout 100% mort** |
| `src/Service/Client/VersionManager.php` | 169 | | Service |
| `src/Controller/*Controller.php` (×4) | 97-153 | | Contrôleurs ressource (dupliqués) |

Aucun fichier 🔴 côté backend ; un seul 🔴 front (`ResourceLoader.vue`).

## 3. Constats DRY / SOLID / KISS

### DRY
- **D1 — `paginate()` dupliqué à l'identique dans les 4 managers** : `ChampionManager.php:281`, `ItemManager.php:146`, `RuneManager.php:93`, `SummonerManager.php:96`. ~30 lignes × 4. Seules variantes : la racine paginée (`['data']` vs liste top-level pour les runes) et le plafond (`20` vs illimité pour summoner).
- **D2 — `dataError()` + `redirectToSetupWithError()` identiques (au caractère près, même typo « Donnés absente »)** dans les 4 contrôleurs : `ChampionController.php:133,144`, `ItemController.php:119,130`, `RuneController.php:77,88`, `SummonerController.php:109,120`. Idem le bloc constructeur (4 dépendances de base communes).
- **D3 — `splitJson()` dupliqué** : `AbstractManager.php:397` et `Utils.php:164` (ce dernier mort → cf. K1).

### SOLID (SRP)
- **S1 — `ClientManager` (413 l.) mêle 4 responsabilités** : préférences en session, crypto du cookie « remember » (HMAC), résolution de la locale UI, et **validation des query params GET** (`getParams/handle*`, `:316-393`). Ce dernier cluster est **mort** (cf. K2). La crypto cookie est un collaborateur naturel (`RememberCookieCodec`).
- **S2 — `AbstractManager` (401 l.)** porte 3 sous-responsabilités (dataset / ingestion images / manifeste). Cohésion acceptable pour une base partagée, mais extractible (`ManifestStore`, `ImageIngestor`). ⚠️ chemin chaud + logique concurrence récemment corrigée → extraction à risque.
- **S3 — `ChampionManager` (312 l.)** : la logique CommunityDragon chroma (`getChromas/fetchChromas/slimChromas/cdragon*`, ~100 l.) est une responsabilité distincte, extractible en `CommunityDragonChromaProvider`.
- **S4 — `ResourceLoader.vue` (717 l.)** : machine à états SSE + orchestration Turbo + template + 300 l. de CSS dans un seul SFC. L'orchestration (EventSource, watchdog, warm-key) et les helpers d'URL purs sont extractibles (composable + module TS testable isolément).

### KISS / code mort
- **K1 — `Utils` (171 l.) entièrement mort** post-migration MinIO : `fileIsExisting/buildDir/buildPath/buildDirAndPath/binaryExisting` = ancien stockage local `upload/{version}/…` (remplacé par le content-addressed) ; `normalizeTag/splitJson` sans appelant ; `generateBackurl()` = **stub vide** (`:168`). Injecté dans `ClientManager` mais **jamais appelé**. Binding dédié dans `services.yaml:35` + paramètre `upload_manager.base_dir:7`.
- **K2 — `ClientManager::getParams()` + `handleVersion/handleLang/handleNumpage/handleItemperpage` (`:316-393`, ~78 l.)** : mini-framework de validation à dispatch magique (`'handle'.ucfirst($need)`), **aucun appelant** (supplanté par `PageContextResolver`).
- **K3 — `HomeController.php`** : pas de `declare(strict_types=1)` (incohérent) ; casts no-op `(string) $language = (string) …` (`:107-109`).
- **K4 — docblocks « tutorial-style »** très verbeux (HomeController, ClientManager) — contraires à la règle « commentaire seulement si valeur ajoutée ». Cosmétique, non traité en masse (bruit/risque).

## 4. Garde-fous disponibles

- **PHP** : `phpunit tests/Unit` → **51 tests verts** (baseline). ⚠️ `tests/Functional/AdminAccessTest` (6) échoue en conteneur `APP_ENV=dev` (`framework.test` inactif) — **pré-existant, sans rapport**.
- **Front** : `npm test` (vitest), `npm run typecheck` (vue-tsc), `npm run build` (vite).
- **Rendu réel** : stack Docker up (`:8080`), toutes les pages **200** — empreinte HTML capturée avant/après pour prouver la préservation.

## 5. Plan de refacto priorisé

| # | Action | Type | Risque | Vérif |
|---|---|---|:---:|---|
| R1 | Supprimer `Utils` (mort) + dép. inutilisée dans `ClientManager` + binding/param `services.yaml` | KISS/dead | **Bas** | phpunit Unit |
| R2 | Supprimer `getParams()/handle*()` morts de `ClientManager` | KISS/dead | **Bas** | grep + phpunit |
| R3 | Nettoyer `HomeController` (`strict_types`, casts no-op) | KISS | **Bas** | phpunit + rendu |
| R4 | Extraire `AbstractResourceController` (deps + `dataError`/`redirect`) pour les 4 contrôleurs | DRY | **Moyen** | phpunit + rendu |
| R5 | Lifter `paginate()` dans `AbstractManager` (seams `paginationCollection`/`perPageCap`) | DRY | **Moyen** | rendu diff |
| R6 | Extraire l'orchestration SSE de `ResourceLoader.vue` (composable + utils) | SRP | **Moyen** | vitest + tsc + build |

**Reco non appliquées (trade-offs explicités en §APRÈS)** : extraction `RememberCookieCodec` (S1), `ManifestStore`/`ImageIngestor` (S2, chemin chaud sensible), `CommunityDragonChromaProvider` (S3).

---

## APRÈS — refacto appliqué (2026-07-17)

Tous les lots ont été exécutés de façon incrémentale, chaque lot vérifié avant le suivant.
Comportement **préservé** (contrats publics inchangés, prouvé par diff de rendu avant/après).

### Ce qui a été corrigé

| # | Action | Résultat |
|---|---|---|
| R1 | Suppression de `Utils` (mort) + dép. inutilisée `ClientManager` + binding/param `services.yaml` | −171 l. mortes ; `debug:container` : plus aucun service `Utils` |
| R2 | Suppression `getParams()/handle*()` morts de `ClientManager` | −78 l. mortes (dispatch magique supprimé) |
| R3 | `HomeController` : `declare(strict_types=1)` + casts no-op supprimés | cohérence stricte |
| R4 | `AbstractResourceController` : deps + `dataError`/`redirectToSetupWithError`/`clientData` mutualisés | 4 contrôleurs allégés, duplication byte-identique supprimée |
| R5 | `paginate()` lifté dans `AbstractManager` (hooks `paginationCollection`/`perPageCap`) | 4 copies (~30 l.) → 1 base ; rendu A/B **byte-identique** |
| R6 | Orchestration SSE de `ResourceLoader.vue` extraite (`loader/urls.ts` + `loader/useLoaderStream.ts`) | SFC 717→**422 l.** (sort du seuil 🔴), template+CSS byte-identiques, spec 9/9 verte |

### Métriques taille (avant → après)

| Fichier | Avant | Après | Δ |
|---|---:|---:|---|
| `ResourceLoader.vue` | 717 🔴 | **422** ⚠️ | −295 (+ `urls.ts` 81, `useLoaderStream.ts` 247) |
| `ClientManager.php` | 413 ⚠️ | **319** ⚠️ | −94 (dead code) |
| `AbstractManager.php` | 401 ⚠️ | **461** ⚠️ | +60 (absorbe 4× `paginate`) |
| `ChampionManager.php` | 312 ⚠️ | **281** | −31 |
| `ItemManager.php` | 177 | **146** | −31 |
| `RuneManager.php` | 124 | **98** | −26 |
| `SummonerManager.php` | 127 | **101** | −26 |
| `Utils.php` | 171 | **0** (supprimé) | −171 |
| 4 contrôleurs ressource | 97–153 | 73–129 | −24 chacun (+ base 62) |

**Plus aucun fichier 🔴** (>500). Dead code éliminé : ~420 lignes. Duplication éliminée : `paginate` (4×) + helpers contrôleur (4×) + `splitJson` (Utils).

### Audit frontend (Playwright — layout + contraste)

10 pages × 2 viewports (desktop 1440 / mobile 390), screenshots + analyse programmatique.

- **Layout / centrage** : bug réel trouvé — le sélecteur de langue du header débordait de **+5px sur mobile** (toutes pages), car les `<select>` `flex-1` ne pouvaient pas rétrécir sous leur contenu (`min-width:auto`). **Corrigé** : selects empilés en mobile, `min-w-0` + `sm:flex-1` dès `sm`. → **0 overflow** sur les 10 pages/2 viewports. (Le glow `.hx-breathe` et les strips de skins `-mx-6` débordent visuellement mais sont clippés — décoratifs intentionnels, pas des bugs.)
- **Contraste (WCAG AA)** : axe-core (0 violation fond plein, mais texte-sur-image « incomplete ») complété par un **échantillonnage pixel réel** (masquage de l'encre + médiane du fond). 20 échecs réels trouvés, 2 tokens fautifs :
  - `--color-text-dim` `#5b5a56` → **`#8f8d82`** : ~2.5:1 → ~5:1 (eyebrows/labels/slots/clés de runes des pages détail — 18 échecs). Reste dimmer que `--color-text-muted` (hiérarchie préservée).
  - `.hx-chip` `bg-void/60` → **`bg-void/90`** : l'art des cartes transparaissait sous les chips de modes summoner (« CLASSIC », ~3.1:1). → ~6:1.
  - **Résultat : 0 échec de contraste** sur les 10 pages (accordéons dépliés = état réellement visible).

### Garde-fous (finaux, tous verts)

- Backend : `phpunit tests/Unit` → **51/51**.
- Front : `vitest` **16/16**, `vue-tsc` **0 erreur**, `vite build` **OK**.
- Rendu réel : 10 pages **200**, diff comportemental nul (hors bruit profiler/nonce).

### CLAUDE.md

Créé à la racine : stack, invariants d'architecture, conventions de code (DRY/KISS/SOLID/CQS + limites 300/500, exemption DI), règles par langage (PHP `strict_types`+`final`, TS/Vue, Go), commentaires (anglais, « pourquoi »), garde-fous, pièges connus.

### Recommandations non appliquées (trade-offs)

Écartées volontairement car **à risque sur du code chaud/récemment corrigé** ou à valeur/coût défavorable ; à faire seulement sur décision explicite :

- **`AbstractManager` → collaborateurs `ManifestStore` + `ImageIngestor`** (S2). Le fichier est désormais le plus gros (461 l.). L'extraction clarifierait le SRP et le ferait redescendre, **mais** touche le chemin chaud + la logique concurrence du manifeste corrigée récemment (A2/B1). Bénéfice réel, risque réel → à isoler avec des tests de concurrence dédiés.
- **`ClientManager` → `RememberCookieCodec`** (S1) : extraire la crypto HMAC du cookie. Propre, mais `ClientManager` sort d'une refonte i18n → différer.
- **`ChampionManager` → `CommunityDragonChromaProvider`** (S3) : la logique chroma (~100 l.) est une responsabilité distincte, extractible sans risque majeur — candidat le plus sûr des trois si on continue.
- **Typo user-facing** « Donnés absente » (flash d'erreur, `AbstractResourceController::dataError`) : préservée à l'identique pour un refacto à comportement nul ; à corriger séparément (« Données absentes »).

> ⚠️ **Hors périmètre de ce refacto** : `README.md` + `docs/assets/` ont été modifiés en parallèle par un autre processus pendant cette session — non inclus ici.
