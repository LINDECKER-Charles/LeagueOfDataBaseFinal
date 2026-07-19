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

---
---

# Passe 2 — 2026-07-19

> Nouveau diagnostic après l'ajout des lots comptes/builds/profil, analytics admin, journal
> d'audit, API publique, SEO. Périmètre : dépôt hors `vendor`, `node_modules`, `var`, `.git`.
> Diagnostic établi par 4 explorations parallèles (contrôleurs · services analytics/build/seo ·
> entités/repos/managers · front Vue/TS/CSS). État **AVANT** ci-dessous ; **APRÈS** en fin de section.

## 1. Tailles hors seuils (⚠️ > 300 · 🔴 > 500)

| Fichier | Lignes | Seuil | Nature |
|---|---:|:---:|---|
| `assets/styles/profile.css` | 978 | 🔴 | 3 systèmes CSS agrégés (picker + édition + carte publique) |
| `src/Service/API/AbstractManager.php` | 486 | ⚠️ | Base managers (data + images + manifeste + pagination) |
| `assets/styles/builds.css` | 447 | ⚠️ | Feuille build (forge) |
| `assets/vue/components/ResourceLoader.vue` | 422 | ⚠️ | SFC loader (déjà refait passe 1) |
| `src/Entity/User.php` | 396 | ⚠️ | Entité (anémique — cf. C13) |
| `src/Controller/BuildController.php` | 393 | ⚠️ | Contrôleur + orchestration écriture build |
| `src/Controller/ProfileController.php` | 349 | ⚠️ | Contrôleur + save favoris/skin |
| `assets/styles/changelog.css` | 334 | ⚠️ | Feuille changelog |
| `templates/champion/detail.html.twig` | 332 | ⚠️ | Template détail champion |
| `src/Service/Client/ClientManager.php` | 319 | ⚠️ | Prefs session + crypto cookie + hydratation |
| `src/Service/API/ChampionManager.php` | 319 | ⚠️ | Manager + chroma CommunityDragon |
| `assets/vue/components/ResourceFilter.vue` | 320 | ⚠️ | SFC : orchestration filtre non extraite |
| `src/Service/Analytics/StorageAnalyticsService.php` | 313 | ⚠️ | Orchestration + agrégation + assemblage rapport |
| `assets/styles/showcase.css` | 304 | ⚠️ | Feuille showcase |

Aucun **PHP 🔴**. Un seul **CSS 🔴** (`profile.css`).

## 2. Constats DRY / SOLID / KISS (extraits — file:line)

### Backend — Managers de ressource (héritent `AbstractManager`)
Duplication verbatim × 4 masquée par des surcharges identiques :
- **getImage()** identique ×4 (`ChampionManager:309`, `ItemManager:239`, `SummonerManager:91`, `RuneManager:88`) + params `$dir`/`$lang` **morts** (imposés par `CategoriesInterface:58`).
- **dataList()/perPageCap()** identiques ×3, **imageEntries()** ×2-3, **getByName()** ×2 + variante scan ×2, **searchByName()** ×2-3 avec garde + magic `2/50` recopiés, **prologue getImages()** ×4, accès `['data'] ?? []` ×8.
- Pattern « read storage → catch → fetch gateway → persist » implémenté 3× (`AbstractManager:126`, `ChampionManager:28`, `ChampionManager:99`).

### Backend — Contrôleurs
- **`currentUser()`** copié à l'identique ×4 (`BuildController:354`, `ProfileController:258`, `ApiKeyController:146`, `BuildVoteController:110`) + variantes inline ×3.
- **Garde CSRF** `isCsrfTokenValid(..., (string)$request->request->get('_token'))` répétée **11×** hors Admin (déjà factorisée côté Admin via `AbstractAdminController::csrfError`).
- **`requireVerifiedEmail()`** quasi identique ×2 ; helper « addFlash error + redirect » réimplémenté ×5 ; `AuditTarget::of(...)` boilerplate ×5.
- **SRP** : `ProfileController::save()` ~51 l./5+ responsabilités, `ChampionController::champion()` ~53 l./3 try-catch, `BuildController` porte l'orchestration d'écriture build.
- `HomeController` n'hérite pas d'`AbstractResourceController` et reconstruit `ClientData::fromServices()` à la main (`:50,:160`).
- **Correctness** : `ChampionController::searchChampionsApi()` renvoie un message d'erreur en **JSON string HTTP 200** (`:123`) — `PickerController:96` fait proprement 503+`{error}`.

### Backend — Services analytics/build/seo
- **Traversée de l'arbre de runes DDragon dupliquée 3×** (`BuildStructureValidator:227`, `BuildStructureProjector:136`, `BuildViewAssembler:219`) + forme « page de runes » (4 clés) énumérée 3×.
- **Construction « lignes rangées + pct »** dupliquée (`StorageAnalyticsService:229`, `RangeReportBuilder:182`, `:161`).
- **`resolve()` des 4 projectors picker** = même squelette copié.
- **SRP** : `StorageAnalyticsService` = orchestration + agrégation pure + assemblage rapport (le split pur/impur existe pour le trafic mais **pas** pour le stockage) ; `SvgChartRenderer` mêle géométrie SVG et formatage de nombres.
- **Code mort** : `BuildStructureValidator::toInt` (`:287`) jamais appelé.

### Backend — Managers/Client — SRP
- **`AbstractManager` (486 l.)** = 4 axes de changement (données / images / **manifeste concurrentiel** / pagination). ⚠️ `saveManifest:461` porte le correctif de concurrence (read-merge-write bypass caches) — **extraction manifeste = risque élevé, à ne pas toucher**.
- **`ClientManager` (319 l.)** reste fourre-tout : prefs session + crypto cookie HMAC + hydratation. Magic `'fallback-secret'` dupliqué (`:131`, `:229`).
- `VersionManager` : pas de `strict_types`/`final` ; filtre versions via `preg_match('/^lol/')` alors que `VERSION_PATTERN` existe.
- **`User` (396 l.)** : **anémique** (getters/setters) — rien à extraire (constat inverse à l'hypothèse).

### Front Vue/TS
- **Lightbox/carousel dupliqué** verbatim (`ChromaStrip:20` ⟷ `SkinGallery:27`) : ~30 l. identiques.
- **3 composables de catalogue lazy** quasi-redondants (`builds/usePickerCatalog`, `picker/usePickerCatalog` — collision de nom —, `picker/useSkinCatalog`) : garde de statut + fetch JSON + mapping recopiés 3×.
- **`webp()`/`initials()`** dupliqués verbatim (`FavoritePicker:145` ⟷ `SkinBannerPicker:135`) + coquille `<dialog>` picker dupliquée.
- `ResourceFilter.vue` porte toute l'orchestration filtre/pagination dans le SFC (pas de composable, pas de spec).
- `colorName()` (logique pure HSL) coincée dans `ChromaStrip.vue:36`.

### Front CSS
- **`profile.css` (978 l. 🔴)** = 3 systèmes séparables : picker réutilisable (~330 l.), édition profil, carte publique `/u/{username}`.
- **141 occurrences de `rgba()`** réencodant des couleurs du design system (incohérent avec le `color-mix(var(--color-*)…)` utilisé ailleurs) ; utilities dupliquées (frame socket vide, initiales ×6, portrait ×3-4, dégradé panneau ×4, bevel 14px) ; `font-family: ui-monospace` en dur ×4 dans `ResourceFilter.vue` (au lieu de `--font-mono`) ; fallback `--color-danger` divergent (`#d44242` vs token `#c24b4b`).

## 3. Plan de refacto (priorisé — impact × effort × risque)

| Lot | Contenu | Type | Risque | Garde-fou |
|---|---|---|:---:|---|
| **L1** | Services purs : suppr. `toInt` mort · `Ranker` partagé · `AbstractOptionsProjector` · `NumberFormatter` · constantes familles storage | DRY/KISS | Bas | phpunit Unit |
| **L2** | `RuneTreeIndex` (VO) → Validator/Projector/Assembler | DRY/SRP | Moyen | phpunit Build |
| **L3** | Managers : lift `getImage`/`dataList`/`perPageCap`/`imageEntries`/`getByName`/`searchByName`/`dataMap` + suppr. params morts | DRY | Moyen | phpunit API + rendu |
| **L4** | Contrôleurs : traits `ResolvesCurrentUser`/CSRF/flashRedirect · `AuditTarget::apiKey()` · `HomeController` hérite du socle | DRY | Bas-moyen | rendu (200 + diff) |
| **L5** | Front : `useLightbox` · `imageThumb.ts` · `fx/colorName.ts`+spec | DRY/SRP | Bas-moyen | vitest + tsc + build |
| **L6** | CSS : split `profile.css` (picker/édition/public) · tokenisation `rgba→color-mix` · utilities partagées | DRY | Bas | build + diff visuel |

**Différé (risque défavorable / valeur faible)** : extraction `ManifestStore` de `AbstractManager` (**risque élevé** — concurrence), `RememberCookieCodec` (ClientManager sort de refonte i18n), split `StorageAnalyticsService`/`BuildViewAssembler` en projectors (moyen — à faire si L1-L6 stabilisés), unification `SafeRefererResolver` (sécurité open-redirect — audit dédié), `User → Favorites` embeddable (faible valeur). `User` : ne rien extraire (anémique).

---

## APRÈS — refacto appliqué (2026-07-19)

Exécution incrémentale, chaque lot vérifié avant le suivant. Comportement **préservé**
(contrats publics inchangés). Deux abstractions du plan initial ont été **écartées à la
lecture du code** (mauvaise abstraction > duplication ponctuelle, cf. règle projet) :
`Ranker` (les 3 « rows+pct » divergent trop : map `int` vs `{objects,bytes}`, tri `count`
vs `bytes`, colonnes distinctes) et `AbstractOptionsProjector` (résolution d'image
positionnelle / id-keyed / name-keyed / arbre — 4 formes réellement différentes).
`NumberFormatter` idem non fait (`bytes` base-1024 « MB » ≠ `humanBytes` binaire « Mio »).

### Ce qui a été corrigé

| Lot | Action | Résultat |
|---|---|---|
| **L1** | `BuildStructureValidator::toInt` mort supprimé ; `slimChromas`→`mapChroma` (imbrication 4→2) ; constantes familles storage ; `HMAC_FALLBACK_SECRET` (littéral dupliqué ×2 → const) ; `strict_types` sur `VersionManager` | dead code + magic values éliminés ; `final` non appliqué à `VersionManager` (stubé par PHPUnit → impossible) |
| **L2** | VO **`RuneTreeIndex`** : une seule traversée `tree.slots[].runes[].id`, consommée par `BuildStructureValidator` (`slotsByTree()`) et `BuildStructureProjector` (`allIds()`) | 2 traversées dupliquées → 1 ; **les deux sémantiques préservées à l'octet** (perks d'un arbre sans id collectés ; slots keyés par tree-id valide seulement). Assembler laissé tel quel (non testé, couplé images) |
| **L3** | Managers : `getImage` **mort** supprimé (0 appelant) ×4 + interface ; `dataList` unifié via `paginationCollection` ; `imageEntries` défaut (champion/item) ; `perPageCap` défaut non borné ; helpers `dataMap`/`assertSearchable` (+ constantes `NAME_MIN/MAX`) ; **cluster pagination extrait** dans le trait `PaginatesResources` (SRP) | duplication verbatim ×4 supprimée ; `AbstractManager` **486→412** (repasse sous le seuil visé, hors 🔴) ; 4 pages liste + 4 détail **200** |
| **L4** | Trait **`ResolvesCurrentUser`** : `currentUser()` (copie byte-identique ×4) mutualisée sur les 2 hiérarchies (`AbstractResourceController` + `AbstractController`) ; imports morts `User` nettoyés | 4 copies → 1 ; conteneur compile, routes auth-gated 302 (pas de 500) |
| **L6** | **`profile.css` 978 🔴** découpé en `profile-picker.css` / `profile-public.css` / `profile-edit.css`, **ordre source préservé** (chaque fichier ré-enveloppé dans son `@layer components` additif) | **plus aucun 🔴** ; concaténation des règles **byte-identique** à l'original (`cmp`), cascade inchangée, `vite build` OK |

### Métriques taille (avant → après)

| Fichier | Avant | Après | Δ |
|---|---:|---:|---|
| `assets/styles/profile.css` | 978 🔴 | **supprimé** | → 288 / 333 / 363 (3 fichiers, 0 🔴) |
| `src/Service/API/AbstractManager.php` | 486 ⚠️ | **412** ⚠️ | −74 (+ trait `PaginatesResources` 127) |
| `src/Service/API/ChampionManager.php` | 319 ⚠️ | **299** | −20 |
| `src/Service/API/ItemManager.php` | 249 | **219** | −30 |
| `src/Service/API/SummonerManager.php` | 102 | **83** | −19 |
| `src/Service/API/RuneManager.php` | 105 | **94** | −11 |
| `src/Service/Build/BuildStructureProjector.php` | 178 | **158** | −20 (+ VO `RuneTreeIndex` 85) |
| `src/Controller/BuildController.php` | 393 ⚠️ | **384** ⚠️ | −9 (+ trait `ResolvesCurrentUser` 27) |
| `src/Controller/ProfileController.php` | 349 ⚠️ | **341** ⚠️ | −8 |
| `src/Controller/ApiKeyController.php` | 206 | **197** | −9 |
| `src/Controller/BuildVoteController.php` | 123 | **112** | −11 |

**Bilan** : le seul fichier 🔴 (`profile.css`) est éliminé ; `AbstractManager` redescend de 486
à 412 malgré l'absorption de logique partagée (grâce à l'extraction pagination) ; duplication
supprimée = `getImage` mort ×4, `dataList`/`imageEntries`/`perPageCap`/guards ×3-4,
`currentUser` ×4, 2 traversées d'arbre de runes, `slimChromas`. Nouveaux fichiers cohésifs :
`PaginatesResources`, `RuneTreeIndex`, `ResolvesCurrentUser` + 3 feuilles profil.

### Garde-fous (finaux, tous verts)

- Backend : `phpunit tests/Unit` → **415/415** (991 assertions).
- Front : `vitest` **126/126**, `vue-tsc --noEmit` **0 erreur**, `vite build` **OK**.
- Rendu réel : listes (`/champions`,`/objects`,`/runes`,`/summoners`) **200** ; détails versionnés
  (champion/objet/rune/summoner) **200** ; routes auth-gated (`/builds/new`,`/profile`) **302** (login, pas 500).
- Preuve de préservation CSS : `cat profile-*.css | cmp - profile.css` (avant enrobage `@layer`) → **byte-identique**.

### Reste recommandé (non fait, par ordre de valeur/risque)

1. **Décomposition contrôleurs write** (S1 `FavoritesSaver`, S2 `ChampionDetailAssembler`, S3 `BuildWriteHandler`) — ferait passer `BuildController`/`ProfileController` sous 300 ; **différé** faute de tests unitaires contrôleur (vérif rendu seule → risque). À coupler avec des tests fonctionnels.
2. **Front L5** (`useLightbox` ChromaStrip⟷SkinGallery, `imageThumb.ts` webp/initials, `fx/colorName.ts`+spec, unification des 3 composables catalogue) — gains DRW réels mais logique d'interaction sans spec dédiée → à faire avec des tests d'abord.
3. **CSS** : tokenisation `rgba()→color-mix(var(--color-*))` (141 occurrences) + utilities partagées (`hx-initials`, `hx-panel-surface`, `hx-socket-empty`) + `--font-mono` dans `ResourceFilter.vue` + fallback `--color-danger` divergent. Mécanique mais **volumineux et à vérif visuelle** → lot dédié.
4. **`StorageAnalyticsService`** : appliquer au stockage le split pur/impur déjà en place pour le trafic (`StorageAggregator` + `StorageReportBuilder`).
5. **Correctness** : `ChampionController::searchChampionsApi()` renvoie un message d'erreur en JSON HTTP 200 (`:123`) — aligner sur `PickerController` (503 + `{error}`). Changement de contrat de réponse → hors « comportement préservé », à traiter comme fix dédié.
6. **Toujours différé (risque élevé)** : `ManifestStore` (concurrence manifeste), `RememberCookieCodec`, `SafeRefererResolver` (open-redirect, audit sécurité).

