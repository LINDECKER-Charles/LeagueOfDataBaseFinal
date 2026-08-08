# Audit de compatibilité des versions Data Dragon

> **Statut : correctifs appliqués + vérifiés (cf. §10).** Les 4 bugs (A/B/C/D)
> décrits ci-dessous ont été corrigés ; ce document conserve le diagnostic
> complet (cause racine, périmètre exhaustif) et documente l'implémentation et
> la vérification end-to-end.

Date : audit 2026-07-16 · correctifs 2026-07-17 · Branche : `feat/archi-refacto`

---

## 1. Résumé exécutif

Sur les **397 versions sélectionnables** exposées par l'application (toutes les
versions Data Dragon numériques de `0.151.2` à `16.14.1` ; les entrées
`lolpatch_*` sont filtrées par `VersionManager::getVersions()`), **quatre**
familles de défauts ont été confirmées. Trois sont corrélées à la **version**,
une à la **langue** (axe orthogonal, cf. bug D) :

| # | Bug | Page(s) | Périmètre | Symptôme | Environnement |
|---|-----|---------|-----------|----------|---------------|
| **A** | `runesReforged.json` inexistant avant le patch 7.22 | `/home` | **180** versions `≤ 7.21.1` | **HTTP 500** | **tous** (prod incluse) |
| **A** | idem | `/runes` (liste) | 180 versions `≤ 7.21.1` | 302 → `/setup` (page inutilisable) | tous |
| **A** | idem | `/rune/{key}` (détail) | 180 versions `≤ 7.21.1` | 302 → `/setup` | tous |
| **B** | Accès Twig non gardés sur des clés absentes des vieilles données champion | `/champion/{name}` (détail) | **33** versions · **2 612** couples (version, champion) | **HTTP 500** | **dev / test uniquement** (`strict_variables`) |
| **C** | `HomeController::home()` sans gestion d'erreur (amplifie A + D) | `/home` | — (cause structurelle) | 500 au lieu d'une dégradation | tous |
| **D** | Langue valide **globalement** mais absente d'une version | `/home` | jusqu'à **348** versions (`ar_AE`) | **HTTP 500** | **tous** (prod incluse) |
| **D** | idem | listes + détails (toutes ressources) | idem | 302 → `/setup` | tous |

**Chiffre marquant (balayage exhaustif version × langue, cf. §9)** : sur les
**11 116** couples (version, langue) sélectionnables, **5 477 (49,3 %)** rendent
`/home` en **HTTP 500**. Seuls **50,7 %** des couples sont sains sur toutes les
pages.

**Pages non impactées** (confirmé de `0.151.2` à `16.14.1`, en `en_US`) : listes
`/champions`, `/objects`, `/summoners` ; détails `/object/{id}`,
`/summoner/{id}`. Toutes rendent `200` sur l'ensemble de la plage de versions.

**Endpoints images** (portrait champion, icône item, sort d'invocateur, passif,
icône de rune *version-less*, splash art direct) : testés du plus récent au plus
ancien → **`200` partout, aucun bug corrélé à la version**.

> **Priorité** : le bug **A** est le seul qui casse en **production** (le
> `fetch` lève une exception quel que soit l'environnement). Le bug **B** ne se
> manifeste qu'avec `strict_variables` actif (dev + test) ; en prod la page se
> rend mais avec des artefacts (splashs de skins cassés).

---

## 2. Méthodologie

Le test a combiné deux approches complémentaires :

1. **Sonde exhaustive de forme (rapide, déterministe)** — pour chacune des 397
   versions, interrogation directe du micro-service Go (`POST :8085/fetch`) du
   statut HTTP des 4 JSON de ressources (`champion`, `item`, `summoner`,
   `runesReforged`) en `en_US`. Combinée à la sémantique d'erreur du code (cf.
   §3), cela donne la matrice complète des crashs de façon déterministe.
2. **Validation sur l'application réelle (vérité terrain)** — rendu HTTP effectif
   des pages via `:8080` sur les versions frontières et sur toute la plage
   ancienne, pour capter les plantages de **rendu Twig** que la sonde JSON ne
   voit pas (dépendants de la forme des données, pas seulement de la présence du
   fichier).

Résultats bruts de la sonde :

- `champion.json`, `item.json`, `summoner.json` : **`200` sur les 397 versions**
  (y compris la plus ancienne `0.151.2`). Ces ressources ne provoquent jamais de
  404.
- `runesReforged.json` : **`200` sur 217 versions** (`7.22.1` → `16.14.1`),
  **`403` sur 180 versions** (`7.21.1` → `0.151.2`). Frontière nette : le fichier
  apparaît à partir du patch **7.22.1**.

Sondes complémentaires (ajoutées après une première passe `en_US` seule) :

- **Axe langue** : les 28 langues annoncées par `/languages`, croisées avec un
  échantillon de versions + une passe frontière (9 langues × 397 versions). A
  révélé le **bug D** (cf. §6).
- **Endpoints images** : portrait/item/sort/passif/icône-rune/splash, testés sur
  la version la plus récente et la plus ancienne → tous `200`, **aucun** défaut.

### Reproduction

```bash
# Frontière runes : 500 sur /home, 302 sur /runes
curl -s -o /dev/null -w '%{http_code}\n' \
  'http://localhost:8080/runes?version=7.21.1&lang=en_US'   # 302 -> /setup
# /home lit la version en SESSION (pas la query) :
J=/tmp/j; rm -f $J
curl -s -o /dev/null -c $J -b $J \
  --data-urlencode version=7.21.1 --data-urlencode langue=en_US \
  http://localhost:8080/setup-submit
curl -s -o /dev/null -w '%{http_code}\n' -b $J http://localhost:8080/home  # 500

# Détail champion (dev/test) : 500 sur les vieilles données
curl -s -o /dev/null -w '%{http_code}\n' \
  'http://localhost:8080/champion/Annie?version=0.151.2&lang=en_US'  # 500 (partype)
curl -s -o /dev/null -w '%{http_code}\n' \
  'http://localhost:8080/champion/Annie?version=3.10.3&lang=en_US'   # 500 (skin.num)
```

---

## 3. Bug A — `runesReforged.json` absent avant le patch 7.22

### 3.1 Cause racine

Le système de runes « Runes Reforged » (`runesReforged.json`) a été introduit au
**patch 7.22** (novembre 2017). Pour toute version antérieure, le CDN Data Dragon
répond **HTTP 403** sur `…/data/{lang}/runesReforged.json`.

La chaîne d'accès aux données ne tolère pas ce non-2xx :

- `GoFetcherClient::fetch()` **lève une `\RuntimeException`** sur tout statut hors
  2xx (`app/src/Service/Tools/GoFetcherClient.php:128-131`, méthode `decodeItem`).
- `AbstractManager::getData()` → `loadOrFetchData()` appelle ce `fetch()` et
  laisse l'exception se propager (`app/src/Service/API/AbstractManager.php:121`).
- L'exception remonte donc hors de `RuneManager::paginate()` et de
  `RuneManager::getByName()`.

L'exception étant levée à l'intérieur du callback de cache
(`ddragonCache->get`), rien n'est mis en cache : **chaque requête reproduit le
crash** de façon déterministe.

### 3.2 Conséquences par page

| Page | Code | Comportement | Explication |
|------|------|--------------|-------------|
| `/home` | **500** | Page d'accueil totalement cassée | `HomeController::home()` appelle `runeManager->paginate()` **sans try/catch** (`app/src/Controller/HomeController.php:184`). L'exception n'est pas rattrapée → 500. |
| `/runes` (liste) | **302** → `/setup` | Liste inaccessible, redirigée vers la page de config avec un flash d'erreur | `RuneController::runes()` **rattrape** l'exception (`RuneController.php:37`) et redirige. |
| `/rune/{key}` (détail) | **302** → `/setup` | Détail inaccessible | `RuneController::rune()` rattrape (`RuneController.php:63`). |

> Les autres previews de `/home` (champion, item, summoner) fonctionnent : ces
> JSON existent sur toutes les versions. C'est **uniquement** l'appel runes qui
> fait tomber la page.

### 3.3 Périmètre (180 versions, `≤ 7.21.1`)

Plus récente cassée : `7.21.1`. Plus ancienne : `0.151.2`. Toutes les versions
`< 7.22.1`. Première version saine : `7.22.1`.

### 3.4 Correctifs proposés

Deux niveaux, complémentaires :

**A-1 — Rendre `/home` résiliente (indispensable, corrige le 500).**
`HomeController::home()` est la seule action qui n'imite pas le patron
try/catch → dégradation des controllers de liste. Chaque preview doit être
indépendante : l'échec d'une ressource ne doit pas tuer la page.

```php
// HomeController::home() — esquisse
$preview = function (callable $fn): array {
    try { return $fn(); } catch (\Throwable) { return []; }
};
$summoners = $preview(fn () => $this->summonerManager->paginate($v, $l, 4, 1));
$items     = $preview(fn () => $this->itemManager->paginate($v, $l, 4, 1));
$champions = $preview(fn () => $this->championManager->paginate($v, $l, 4, 1));
$runes     = $preview(fn () => $this->runeManager->paginate($v, $l, 4, 1)); // [] avant 7.22
```
Le template `home/home.html.twig` devra tolérer une preview vide (à vérifier :
sections déjà gardées par `is defined`/`is not empty`).

**A-2 — Traiter « ressource absente pour cette version » comme un jeu vide,
pas comme une erreur (recommandé).** Une version pré-7.22 sans runes n'est pas
une panne : c'est une absence légitime. On peut distinguer, dans la couche
données, un **403/404 définitif** (ressource inexistante → renvoyer `[]`) d'une
**erreur transitoire** (5xx/timeout → continuer à lever). Cela rend la liste
`/runes` gracieuse (état vide explicite) au lieu de rebondir sur `/setup`.

> ⚠️ *Trade-off* : ne pas mettre en cache un `[]` issu d'une panne transitoire
> (sinon une version valide resterait « vide » 1h). Le distinguo doit porter sur
> le **statut HTTP** (403/404 = absent, cacheable ; le reste = throw).
> `GoFetcherClient` expose déjà le statut : il faut un chemin qui le remonte sans
> lever, p.ex. une variante `tryFetch(): ?string` ou un code d'erreur typé.

**A-3 — UX (optionnel).** Signaler côté interface que les runes n'existent pas
avant 7.22 (état vide expliqué), voire masquer l'entrée « Runes » / la preview
runes quand la version sélectionnée est antérieure. Évite de proposer une
navigation qui mène à une impasse.

---

## 4. Bug B — Détail champion : accès Twig non gardés sur vieilles données

### 4.1 Cause racine

`templates/champion/detail.html.twig` accède à plusieurs clés **sans garde**
(`is defined` / `?? ` / `|default`). Sur les données champion anciennes, ces
clés sont absentes. Avec `strict_variables` actif, Twig lève alors
`Key "…" does not exist` → **500**.

`strict_variables` vaut `%kernel.debug%` par défaut (fixé explicitement à `true`
`when@test` dans `config/packages/twig.yaml`). Donc :

| Environnement | `strict_variables` | Détail champion vieille version |
|---------------|--------------------|---------------------------------|
| **test**      | `true` (explicite) | **500** |
| **dev**       | `true` (= debug)   | **500** |
| **prod**      | `false` (= debug off) | Se rend, mais artefacts (voir 4.4) |

Accès fautifs identifiés (tous dans `champion/detail.html.twig`) :

| Ligne | Expression | Absente sur | Vérifié |
|-------|-----------|-------------|---------|
| **48** | `{% if champion.partype %}` | données major `0.x` (`partype` ajouté au patch ~3.8) | ✅ crash `0.151.2` |
| **167** | `{% if skin.num != 0 %}` | skins des vieux patchs (skins `{id,name}` sans `num`) | ✅ crash `3.10.3` |
| 42 | `{% if champion.title %}` | (présent partout dans l'échantillon) | latent |
| 89–92 | `champion.info.attack/defense/magic/difficulty` | (présent partout) | latent |

> `champion.name`, `champion.tags|default([])`, `champion.stats is defined`,
> `champion.lore|default(...)`, blocs `passive`/`spells`/`allytips`/`enemytips`
> sont **déjà** gardés → sains.

### 4.2 Interaction avec `getDetail()` (important)

`ChampionController::champion()` fusionne le résumé (`champion.json`) avec le
détail complet (`champion/{name}.json`) dans un **second** try/catch
(`ChampionController.php:71-79`). Si le fichier de détail par champion est absent
(certains vieux patchs renvoient **403** dessus), la fusion est ignorée et la
page se rend sur le **résumé seul** :

- Résumé seul ⇒ `champion.skins` **non défini** ⇒ bloc skins sauté ⇒ pas de
  crash `skin.num`.
- Le crash `skin.num` ne survient donc **que** lorsque le fichier de détail
  existe (`200`) ET que ses skins n'ont pas de `num`.

C'est pourquoi la liste des versions cassées est **discontinue** dans les
`3.x` (elle suit la disponibilité des fichiers de détail par champion sur le
CDN).

### 4.3 Périmètre — balayage exhaustif tous champions (33 versions, 2 612 couples)

Un premier relevé limité à **Annie** avait donné 20 versions ; il **sous-estimait**
le périmètre (le fichier détail d'Annie est absent — `403` — sur plusieurs
patchs où *d'autres* champions crashent). Le balayage **champion par champion**
sur les majors `0` et `3` (validé point par point sur l'app, cf. §9) donne :

- **2 612** couples (version, champion) provoquant un `500`, sur **33 versions**.
- **major-0 (9 versions)** : `partype` absent au niveau du résumé → **tous** les
  champions crashent (107–110 champions chacun).
- **major-3** : trois régimes selon la disponibilité du fichier détail par
  champion (qui conditionne le bloc skins) :
  - **ALL** (7 v. : `3.10.2/.3`, `3.12.2/.24/.26/.33/.34`) — détail servi pour
    tous → `skin.num` fait tomber tout le monde.
  - **partiel** (17 v. : `3.6.14/.15`, `3.7.1/.2/.9`, `3.8.1/.3/.5`, `3.9.4/.5`,
    `3.10.6`, `3.11.2`, `3.12.36/.37`, `3.13.1/.6/.8`) — seuls les champions dont
    le détail est servi **et** qui ont > 1 skin sans `num` crashent (de 2/116 à
    103/114 selon le patch).
  - **clean** malgré l'ancienneté (`3.9.7`, `3.11.4`) — tous les détails `403` →
    rendu sur le résumé seul → bloc skins ignoré → pas de crash.
- **≥ `3.13.24` et majors `4.x`+** : **sains** (les skins portent `num`, `partype`
  présent) — 0 crash même quand beaucoup de fichiers détail sont `403`.

> Conséquence pratique : la plage de crash est **stable au niveau version**
> (`0.x` + `3.x` ≤ `3.13.8`), mais l'ensemble exact des champions touchés varie
> par patch. Les 9 versions major-0 sont **intégralement** cassées (n'importe
> quel champion).

### 4.4 Comportement en production (`strict_variables=false`)

La page **ne plante pas**, mais :

- Bloc `partype` (L48) : `null` → condition fausse → simplement omis (bénin).
- Skins (L167) : `skin.num` → `null` → l'URL de splash devient
  `…/splash/{id}_.jpg` (numéro vide) → **toutes les vignettes de skins cassées**,
  et `null != 0` étant vrai, le skin « default » n'est plus filtré.

Donc même sans le 500, ces versions ont un **rendu dégradé** en prod → à
corriger malgré tout.

### 4.5 Correctifs proposés

Garder chaque accès (indépendant de `strict_variables`, corrige aussi le rendu
prod) :

```twig
{# L48 #}
{% if champion.partype is defined and champion.partype %}

{# L166-167 : filtrer par index quand num est absent, ou garder num #}
{% for skin in champion.skins %}
  {% set skinNum = skin.num ?? loop.index0 %}
  {% if skinNum != 0 %}
    <img src="{{ splashBase }}/{{ champId }}_{{ skinNum }}.jpg" …>
  {% endif %}
{% endfor %}

{# L42 (latent) #}
{% if champion.title is defined and champion.title %}
```

**Recommandation transverse** : ajouter un **test fonctionnel** qui rend les 4
pages de détail sur une version ancienne (p.ex. `0.151.2` et `3.10.3`). Comme
`strict_variables=true` `when@test`, ce test aurait attrapé les deux crashs. À
défaut, tout nouvel accès non gardé restera invisible jusqu'à la prod-dégradée.

---

## 5. Bug C — `HomeController` sans stratégie d'erreur (cause structurelle)

Tous les controllers de **liste** (`Champion/Item/Rune/Summoner`) enveloppent
leur `paginate()` dans un `try/catch` qui dégrade proprement vers `/setup`
(`redirectToSetupWithError`). `HomeController::home()` est la **seule** action
qui appelle les managers **sans aucune protection** (`HomeController.php:180-184`).

Conséquence : la page la plus visitée est aussi la plus fragile — n'importe
quelle défaillance d'**une** des 4 ressources (indisponibilité CDN, version sans
runes, timeout Go) renvoie un **500** intégral au lieu d'une dégradation.

Le correctif **A-1** (isolation par preview) résout à la fois le bug A sur
`/home` et cette fragilité structurelle. À traiter en priorité.

---

## 6. Bug D — Langue valide globalement mais absente d'une version

### 6.1 Cause racine

`/languages` (passthrough Go de `cdn/languages.json`) renvoie la liste des
langues de la **dernière** version — **28 langues**. `VersionManager` valide
ensuite toute sélection contre cette liste **globale**, jamais par version :

- `languageExists()` (`VersionManager.php:134-144`) : `in_array($lang, getLanguages())`.
- `validateSelection()` (POST `/setup-submit`) et
  `PageContextResolver::selection()` / `ClientManager::getSession()` s'appuient
  dessus.

Or une langue récente n'existe pas sur les anciennes versions du CDN. La
sélection passe donc la validation, puis le `fetch` de
`data/{lang}/{ressource}.json` renvoie **404** → `GoFetcherClient::fetch()`
**lève** → **exactement la même propagation que le bug A** (`/home` sans
try/catch → 500 ; listes/détails → 302).

C'est un axe **orthogonal** à la version : il touche même des versions récentes
qui, elles, ont bien leurs runes (p.ex. `ar_AE` casse `13.x`).

### 6.2 Surface (exhaustif : 28 langues × 397 versions, cf. §9)

| Langue | Dispo sur | Versions cassées | Plus récente version cassée |
|--------|-----------|------------------|------------------------------|
| `ar_AE` (arabe) | 49 / 397 | **348** | `≤ 14.13.1` (OK dès `14.14.1`) |
| `vi_VN` (vietnamien) | 85 / 397 | **312** | `≤ 12.23.1` (OK dès `13.1.1`) |
| `id_ID` (indonésien) | 251 / 397 | ~146 | disponibilité **non contiguë** |
| `es_AR` | 382 / 397 | ~15 (anciennes) | `≤ 3.8.1` |
| `ja_JP` (japonais) | 384 / 397 | 13 | serveur JP lancé fin 2016 |
| `en_AU`, `es_MX` | 388–391 / 397 | 6–9 (tail major-0) | — |
| `pt_BR`, `ru_RU`, `zh_CN`, `en_US`, + 16 autres | 397 / 397 | aucune | `en_US` = seul garanti partout |

Mesure **désormais exhaustive** : les 28 langues × 397 versions ont été balayées
(cf. §9). 7 langues sont incomplètes ; les 21 autres couvrent les 397 versions.

### 6.3 Vérité terrain (app, version `5.1.1`)

| Requête | Code | Note |
|---------|------|------|
| `/champions?version=5.1.1&lang=ar_AE` | **302** | langue absente → throw → redirect |
| `/champions?version=5.1.1&lang=fr_FR` | 200 | langue présente → OK (isole bien l'axe langue) |
| `/champion/Annie?version=5.1.1&lang=vi_VN` | **302** | détail dégradé (données absentes) |
| `/home` (session `version=5.1.1, lang=ar_AE`) | **500** | idem bug A, pas de try/catch |

> Sur les détails, l'axe langue produit un **302** (données introuvables dans le
> 1er try/catch), pas le 500 `strict_variables` du bug B — les deux sont
> distincts.

### 6.4 Correctifs proposés

- **D-1** — Les correctifs **A-1** (résilience `/home`) et **A-2** (404/403
  définitif → jeu vide non cacheable si transitoire) **corrigent aussi le bug
  D** : bugs A et D empruntent le même chemin d'erreur. C'est le levier
  principal.
- **D-2 — Validation par version.** Restreindre les langues sélectionnables à
  celles réellement disponibles pour la version choisie. DDragon n'expose pas de
  `languages.json` par version : la liste par version se **dérive** (sonder les
  ressources, ou tenir une table). Alternative pragmatique : rendre le sélecteur
  de langue dépendant de la version, et retomber en `en_US` (toujours présent)
  quand la langue demandée est absente pour la version — plutôt qu'échouer.
- **D-3 — Fallback `en_US`.** `en_US` est présent sur les 397 versions : un
  fallback silencieux vers `en_US` (avec avertissement UI) élimine tout crash de
  cet axe sans bloquer l'utilisateur.

---

## 7. Plan de correction priorisé (✅ appliqué — cf. §10)

| Prio | Action | Corrige | Fichier(s) | Risque |
|------|--------|---------|-----------|--------|
| **P0** | Isoler chaque preview de `/home` par try/catch (A-1) | Bug A + **D** (`/home` 500) + Bug C | `HomeController.php` | faible |
| **P1** | 403/404 définitif → jeu vide (non cacheable si transitoire) (A-2) | Bug A + **D** (listes/détails) | `GoFetcherClient.php`, `AbstractManager.php` | moyen (distinguer absent vs panne) |
| **P1** | Garder `partype` (L48), `skin.num` (L167), `title` (L42) | Bug B (500 dev/test + rendu prod dégradé) | `templates/champion/detail.html.twig` | faible |
| **P1** | Fallback `en_US` quand la langue est absente pour la version (D-3) | Bug D | `PageContextResolver` / `ClientManager` / `VersionManager` | faible |
| **P2** | Validation des langues **par version** (D-2) | Bug D (à la racine) | `VersionManager` + sélecteur front | moyen (pas de `languages.json` par version) |
| **P2** | Test fonctionnel « détails sur version ancienne + langue tardive » | Non-régression B & D | `tests/` | faible |
| **P3** | UX : état vide runes / langues indispo expliqués (A-3) | Confort | templates + front | faible |

> Ces actions ont été **appliquées et vérifiées** (§10). La validation par
> version (D-2, table de langues par version) et l'UX état-vide (A-3, P3) restent
> optionnelles : le repli `en_US` (D-3) neutralise déjà tout crash de l'axe langue.

---

## 8. Couverture de test (honnêteté du périmètre)

Ce qui a été réellement exercé, pour éviter toute sur-affirmation :

| Endpoint / ressource DDragon exploité | Couverture | Détail |
|----------------------------------------|-----------|--------|
| `data/{lang}/champion.json` | **397 versions × 28 langues** | exhaustif (matrice complète) |
| `data/{lang}/item.json` | **397 versions** (en_US) + atomicité langue prouvée | présence langue = celle du dossier (proxy validé) |
| `data/{lang}/summoner.json` | **397 versions × 28 langues** | exhaustif (proxy de présence de dossier) |
| `data/{lang}/runesReforged.json` | **397 versions** (en_US) | exhaustif version (indépendant langue) |
| `data/{lang}/champion/{name}.json` (détail) | **majors 0 & 3 : tous les champions** (2 612 couples) + 4.x/5.x vérifiés sains | exhaustif sur la plage à risque |
| `img/champion/{name}.png` | newest + oldest | 200 ; pas de balayage complet |
| `img/item/{name}.png` | newest + oldest | 200 |
| `img/spell/{name}.png` (sort d'invocateur) | newest + oldest | 200 |
| `img/passive/{name}.png`, `img/spell/*` (sorts champion) | newest + oldest (Annie) | 200 |
| `cdn/img/…` (icônes de runes, version-less) | 1 échantillon | 200 |
| `img/champion/splash/{id}_{n}.jpg` (splash direct navigateur) | 1 échantillon | 200 |
| `/api/{res}/search/{name}` (autocomplete) | **non testé** | même couche managers (session) ; 302/erreur JSON attendus sur versions/langues cassées |
| `/api/loader/prepare` (SSE `LoaderController`) | **non testé** | réutilise `collectPlan`/`getData` → mêmes causes A/D attendues |

**Reste non couvert** (faible valeur ajoutée) : le balayage complet des
**endpoints images** par version (échantillon récent+ancien = 200, patterns
d'URL stables), le détail item/summoner par entité (templates prouvés
défensifs, `200` sur toute la plage), et les endpoints `/api/*` search + SSE
loader (mêmes managers, mêmes causes racines A/D). Le cœur — matrice
version × langue et crash détail champion — est **exhaustif**.

---

## 9. Balayage exhaustif — synthèse chiffrée

### 9.1 Matrice complète version × langue (11 116 couples)

Les 397 versions × 28 langues ont été balayées (présence du dossier
`data/{lang}/` via `summoner.json`, atomicité prouvée : 0 divergence sur 40
triples summoner/champion/item échantillonnés). Croisée avec la disponibilité
version-gated de `runesReforged`, cela donne le nombre exact de couples cassés
**par page** :

| Page(s) | Couples cassés / 11 116 | % | Condition |
|---------|-------------------------|---|-----------|
| listes + détails champion/item/summoner | **849** | 7,6 % | dossier langue absent → `302` |
| `/runes` + `/rune/{key}` | **5 477** | 49,3 % | langue absente **ou** version < 7.22 → `302` |
| **`/home`** | **5 477** | **49,3 %** | idem → **`500`** |
| **Sains sur toutes les pages** | **5 639** | **50,7 %** | — |

Décomposition du `/home` 500 : 180 versions < 7.22 × 28 langues = 5 040 couples
(cassés quelle que soit la langue) + 437 couples version ≥ 7.22 mais langue
absente.

### 9.2 Détail champion — balayage tous champions (2 612 couples)

Majors 0 & 3 balayés champion par champion (fichier `champion/{id}.json` de
chaque champion de chaque version) ; majors 4.x/5.x vérifiés sains.

- **2 612** couples (version, champion) → `500` (dev/test), sur **33 versions**.
- 9 versions major-0 **intégralement** cassées (`partype`) ; 24 versions major-3
  ALL/partiel (`skin.num`).

### 9.3 Validations sur l'application (vérité terrain)

Le modèle dérivé des données a été confronté à l'app pour des cas **non-Annie** :

| Requête | Attendu | Obtenu |
|---------|---------|--------|
| `/champion/Aatrox?version=3.13.1` | 500 (skin.num) | ✅ 500 |
| `/champion/Jinx?version=3.12.37` | 500 (skin.num) | ✅ 500 |
| `/champion/Annie?version=3.12.37` | 200 (non-crasher) | ✅ 200 |
| `/champion/Aatrox?version=3.11.4` | 200 (détail 403 → résumé) | ✅ 200 |
| `/champion/Ashe?version=0.151.2` | 500 (partype) | ✅ 500 |
| `/home` session(`5.1.1`,`ar_AE`) | 500 (bug D) | ✅ 500 |
| `/champions?version=5.1.1&lang=ar_AE` | 302 | ✅ 302 |

Concordance **7/7**. Les chiffres dérivés sont donc fiables.

---

## 10. Correctifs appliqués + vérification

### 10.1 Implémentation

Principe : traiter l'absence définitive (403/404) **à la racine** — le chemin
d'erreur commun aux bugs A et D — plutôt que de rustiner chaque page.

| Fichier | Changement |
|---------|-----------|
| `src/Service/Tools/UpstreamNotFoundException.php` *(nouveau)* | Exception typée « ressource définitivement absente » (extends `RuntimeException`). |
| `src/Service/Tools/GoFetcherClient.php` | `fetch()` lève `UpstreamNotFoundException` sur **403/404**, `RuntimeException` sur les autres non-2xx (transitoire). |
| `src/Service/API/AbstractManager.php` | `getData()` : sur absence définitive → repli `en_US` si seule la **langue** manque, sinon **jeu vide** ; les deux persistés. Transitoire (5xx) propagé (jamais figé en vide). **Corrige A + D à la source.** |
| `src/Service/API/ChampionManager.php` | `getDetail()` : détail par champion `403` → résumé seul (plus d'exception). |
| `src/Controller/HomeController.php` | `home()` : chaque preview isolée (`preview()`), forme vide complète compatible `strict_variables`. **Corrige C** + durcit contre les pannes transitoires. |
| `templates/champion/detail.html.twig` | Gardes `champion.title`, `champion.partype` ; skins : `skin.num ?? loop.index0` (repli positionnel = numérotation correcte des splashs). **Corrige B** (+ rendu prod des splashs). |
| `tests/Unit/.../GoFetcherClientTest.php`, `AbstractManagerDataResolutionTest.php` *(nouveau)* | Non-régression : 403/404 → `NotFound`, 5xx → générique ; jeu vide / repli langue / propagation transitoire. |

> `/rune/{key}` sur une version sans runes reste un `302` → `/setup` : c'est le
> comportement « entité inexistante » (getByName sur jeu vide), pas un crash. La
> **liste** `/runes`, elle, dégrade désormais en état vide. Un vrai `404` de
> détail serait une amélioration UX transverse hors périmètre de ce correctif.

### 10.2 Vérification

- **Suite PHPUnit** : **54 tests / 93 assertions verts** (`APP_ENV=test`), dont
  5 nouveaux.
- **Bug A** (end-to-end) : `/home` + `/runes` = `200` sur 12 versions < 7.22
  réparties sur toutes les époques (0.x → 7.21.1). 0 échec.
- **Bug B** : les **66** vrais couples (version, champion) crasheurs (1–2 par
  version sur les 33 versions du balayage §9.2) = `200`. 0 échec.
- **Bug D** : `200` sur 8 couples (version, langue-absente) — `ar_AE`/`vi_VN`/
  `en_AU`/`es_MX`/`id_ID`/`ja_JP` sur versions anciennes → repli `en_US`.
- **Non-régression** : versions saines (`16.14.1` home/détail/runes, `0.151.2`
  `/objects`) inchangées à `200`.

---

## Annexe — Données de référence

- Versions sélectionnables : **397** (`lolpatch_*` exclues par `VersionManager`).
  Plus récente `16.14.1`, plus ancienne `0.151.2`.
- `runesReforged.json` : `200` sur **217** (`≥ 7.22.1`), `403` sur **180**
  (`≤ 7.21.1`).
- `champion.json` / `item.json` / `summoner.json` : `200` sur les **397**.
- Frontière `partype` (données champion) : présent dès `~3.8`, absent sur les
  9 versions major `0.x`.
- Frontière `skin.num` : présent dès `~3.13.24` ; absent avant.
- Crashs `/champion/{name}` (tous champions, majors 0 & 3) : **33** versions ·
  **2 612** couples (version, champion). Major-0 intégralement cassé.
- Matrice version × langue : **11 116** couples ; **/home 500 sur 5 477 (49,3 %)** ;
  sains partout : 5 639 (50,7 %).
- Langues (`/languages`) : **28** annoncées globalement ; disponibilité par
  version — `ar_AE` 49/397, `vi_VN` 85/397, `id_ID` 251/397, `es_AR` 382,
  `ja_JP` 384, `en_AU` 388, `es_MX` 391 ; 21 langues à 397/397. `en_US` est le
  seul garanti partout **et** utilisable comme fallback sûr.
- Endpoints images : `200` du plus récent au plus ancien (aucun bug).
- Sémantique clé : `GoFetcherClient::fetch()` **lève** sur non-2xx
  (`GoFetcherClient.php:128-131`) ; les controllers de liste rattrapent,
  `HomeController` non. Bugs A (version) et D (langue) empruntent ce même chemin.
