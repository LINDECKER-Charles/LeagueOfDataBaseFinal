# Diff SEO : la prod face à la réécriture (L3.13)

- **Date** : 2026-09-26.
- **Prod** : https://league-of-data-base.com, lue en GET seul, sans cookie, une requête à la fois.
- **Réécriture** : http://localhost:18180 (emplacement e1), dernière version 16.19.1.
- **Échantillon** : 42 URLs de la prod (`tools/next/seo-diff/lib/sample.mjs`).
- **Commande** : `node tools/next/seo-diff/diff.mjs --next http://localhost:18180 --stack "emplacement e1"`.

Chaque URL de la prod est demandée à la prod, puis à la réécriture sous sa forme héritée :
la 301 de L3.12 désigne la page comparée. Rapport généré : ne pas le modifier à la main,
ajouter une règle argumentée dans `tools/next/seo-diff/lib/rules.mjs` et relancer l'outil.

## Méthode

- **301** : la forme héritée répond en une seule 301, vers une page de même clé (type, id,
  version), qui répond 200.
- **Titre** et **description** : comparés tels quels, entités HTML décodées.
- **Canonique** : réduite à sa clé (type de page, id, version ; la dernière version vaut
  `latest`), l’origine et la grammaire d’URL mises de côté. Côté réécriture, la canonique
  doit en plus être unique, sans query, et dans la locale de la page.
- **Types JSON-LD** : ensemble des `@type`, imbriqués compris.
- **Champs JSON-LD** : chaque bloc est aplati en `chemin → valeur`, après réécriture des URL
  du site en clés de page (`asset:` et le chemin pour un fichier) ; toute feuille différente
  ou absente d’un côté est un écart.

## Synthèse

| Champ | identiques | écarts expliqués | défauts consignés | non expliqués |
|---|---:|---:|---:|---:|
| 301 | 42 | 0 | 0 | 0 |
| Titre | 39 | 3 | 0 | 0 |
| Description | 38 | 4 | 0 | 0 |
| Canonique | 42 | 0 | 0 | 0 |
| Types JSON-LD | 42 | 0 | 0 | 0 |
| Champs JSON-LD | 29 | 12 | 1 | 0 |

**Verdict** : 0 écart(s) non expliqué(s), 1 défaut(s) consigné(s) pour le jalon du lot 3.

## Résultats par URL

`=` identique, `≈` écart expliqué, `⚠` défaut consigné, `✗` écart non expliqué.

| URL de la prod | Page comparée | 301 | Titre | Description | Canonique | Types JSON-LD | Champs JSON-LD |
|---|---|:---:|:---:|:---:|:---:|:---:|:---:|
| `/` | `/en/` | = | = | = | = | = | = |
| `/champions` | `/en/champions` | = | = | = | = | = | ≈ [list-first-page](#list-first-page) |
| `/objects` | `/en/items` | = | = | = | = | = | ≈ [item-list-index-urls](#item-list-index-urls), [list-first-page](#list-first-page) |
| `/runes` | `/en/runes` | = | = | = | = | = | = |
| `/summoners` | `/en/summoners` | = | = | = | = | = | ≈ [list-first-page](#list-first-page) |
| `/champion/Annie` | `/en/champions/Annie` | = | = | = | = | = | = |
| `/champion/MonkeyKing` | `/en/champions/MonkeyKing` | = | = | = | = | = | = |
| `/champion/Nunu` | `/en/champions/Nunu` | = | = | = | = | = | ≈ [json-ld-html-escaping](#json-ld-html-escaping) |
| `/champion/KSante` | `/en/champions/KSante` | = | = | = | = | = | ≈ [json-ld-html-escaping](#json-ld-html-escaping) |
| `/champion/Belveth` | `/en/champions/Belveth` | = | = | = | = | = | ≈ [json-ld-html-escaping](#json-ld-html-escaping) |
| `/object/1001` | `/en/items/1001-boots` | = | = | = | = | = | = |
| `/object/3031` | `/en/items/3031-infinity-edge` | = | = | = | = | = | = |
| `/object/2003` | `/en/items/2003-health-potion` | = | = | = | = | = | = |
| `/object/3340` | `/en/items/3340-stealth-ward` | = | = | = | = | = | = |
| `/object/1004` | `/en/items/1004-faerie-charm` | = | = | = | = | = | = |
| `/object/771004` | `/en/items/771004-faerie-charm` | = | = | = | = | = | ≈ [item-description-fallback](#item-description-fallback) |
| `/rune/Domination` | `/en/runes/8100-domination` | = | = | = | = | = | = |
| `/rune/Precision` | `/en/runes/8000-precision` | = | = | = | = | = | = |
| `/rune/Inspiration` | `/en/runes/8300-inspiration` | = | = | = | = | = | = |
| `/summoner/SummonerFlash` | `/en/summoners/SummonerFlash` | = | = | = | = | = | = |
| `/summoner/SummonerFlash_Jade` | `/en/summoners/SummonerFlash_Jade` | = | = | = | = | = | = |
| `/summoner/SummonerSmite` | `/en/summoners/SummonerSmite` | = | = | = | = | = | = |
| `/summoner/SummonerSnowball` | `/en/summoners/SummonerSnowball` | = | = | = | = | = | = |
| `/16.14.1/champions` | `/en/16.14.1/champions` | = | = | = | = | = | ≈ [list-first-page](#list-first-page) |
| `/16.14.1/objects` | `/en/16.14.1/items` | = | = | = | = | = | ≈ [item-list-index-urls](#item-list-index-urls), [list-first-page](#list-first-page) |
| `/16.14.1/champion/Aatrox` | `/en/16.14.1/champions/Aatrox` | = | = | = | = | = | = |
| `/16.14.1/object/3031` | `/en/16.14.1/items/3031-infinity-edge` | = | = | = | = | = | = |
| `/16.14.1/rune/Domination` | `/en/16.14.1/runes/8100-domination` | = | = | = | = | = | = |
| `/16.14.1/summoner/SummonerFlash` | `/en/16.14.1/summoners/SummonerFlash` | = | = | = | = | = | = |
| `/15.24.1/champion/Annie` | `/en/15.24.1/champions/Annie` | = | = | = | = | = | = |
| `/champions?lang=fr_FR` | `/fr/champions` | = | ≈ [locale-in-url](#locale-in-url) | ≈ [locale-in-url](#locale-in-url) | = | = | ≈ [locale-in-url](#locale-in-url), [list-first-page](#list-first-page) |
| `/champion/Annie?lang=fr_FR` | `/fr/champions/Annie` | = | ≈ [locale-in-url](#locale-in-url) | ≈ [locale-in-url](#locale-in-url) | = | = | ≈ [locale-in-url](#locale-in-url) |
| `/object/3031?lang=ko_KR` | `/ko/items/3031-infinity-edge` | = | ≈ [locale-in-url](#locale-in-url) | ≈ [locale-in-url](#locale-in-url) | = | = | ≈ [locale-in-url](#locale-in-url) |
| `/champion/Ahri?lang=en_GB` | `/en/champions/Ahri?lang=en_GB` | = | = | = | = | = | = |
| `/about` | `/en/about` | = | = | = | = | = | = |
| `/about/data` | `/en/about/data` | = | = | ≈ [prerendered-data-page](#prerendered-data-page) | = | = | ⚠ [prerendered-data-page](#prerendered-data-page), [dataset-languages](#dataset-languages) |
| `/faq` | `/en/faq` | = | = | = | = | = | = |
| `/changelog` | `/en/changelog` | = | = | = | = | = | = |
| `/legal/notice` | `/en/legal/notice` | = | = | = | = | = | = |
| `/legal/privacy` | `/en/legal/privacy` | = | = | = | = | = | = |
| `/legal/terms` | `/en/legal/terms` | = | = | = | = | = | = |
| `/legal/cookies` | `/en/legal/cookies` | = | = | = | = | = | = |

## Écarts expliqués

### `locale-in-url`

**décision** — ADR 0005 : la locale d'interface est dans l'URL. La prod n'a qu'une URL par page et sert ses textes SEO en anglais aux robots (locale de session ou de cookie) ; `?lang=` n'y change que la langue des données. La cible traduit titre, description, noms du fil d'Ariane et du graphe du site, et `inLanguage`.

- `/champions?lang=fr_FR` — Titre : `League of Legends champions — League Of Data Base` → `Champions de League of Legends — League Of Data Base`
- `/champions?lang=fr_FR` — Description : `All 173 League of Legends champions with roles, abilities and lore — data from patch 16.19…` → `Les 173 champions de League of Legends avec rôles, sorts et histoire — données du patch 16…`
- `/champions?lang=fr_FR` — Champs JSON-LD `@graph.@graph[0].description` : `League of Data Base is a community-driven platform that provides detailed information, sta…` → `League of Data Base est une plateforme communautaire qui fournit des informations détaillé…` (et 4 autre(s))
- `/champion/Annie?lang=fr_FR` — Titre : `Annie, LoL champion — League Of Data Base` → `Annie, champion de LoL — League Of Data Base`
- `/champion/Annie?lang=fr_FR` — Description : `Annie in League of Legends: abilities, skins, lore and tips, up to date for patch 16.19.1.` → `Annie dans League of Legends : sorts, skins, histoire et astuces, à jour pour le patch 16.…`
- `/champion/Annie?lang=fr_FR` — Champs JSON-LD `@graph.@graph[0].description` : `League of Data Base is a community-driven platform that provides detailed information, sta…` → `League of Data Base est une plateforme communautaire qui fournit des informations détaillé…` (et 5 autre(s))
- `/object/3031?lang=ko_KR` — Titre : `무한의 대검, LoL item — League Of Data Base` → `무한의 대검 (LoL 아이템) — League Of Data Base`
- `/object/3031?lang=ko_KR` — Description : `무한의 대검 in League of Legends: stats, cost, recipe and upgrades, up to date for patch 16.19.…` → `리그 오브 레전드의 무한의 대검: 스탯, 가격, 조합식, 상위 아이템. 패치 16.19.1 기준 최신 정보.`
- `/object/3031?lang=ko_KR` — Champs JSON-LD `@graph.@graph[0].description` : `League of Data Base is a community-driven platform that provides detailed information, sta…` → `League of Data Base는 League of Legends에 대한 상세 정보, 스탯, 인사이트를 제공하는 커뮤니티 기반 플랫폼입니다. 빠르고 신뢰할 수…` (et 6 autre(s))

### `list-first-page`

**décision** — ADR 0005 (listes) et L3.4 : l'`ItemList` décrit la première page rendue par le serveur, 12 cartes, dans la limite de 20 ; la prod listait les 20 premières entrées du jeu de données complet.

- `/champions` — Champs JSON-LD `ItemList.itemListElement[12].@type` : `ListItem` → (absent) (et 32 autre(s))
- `/objects` — Champs JSON-LD `ItemList.itemListElement[12].@type` : `ListItem` → (absent) (et 32 autre(s))
- `/summoners` — Champs JSON-LD `ItemList.itemListElement[12].@type` : `ListItem` → (absent) (et 32 autre(s))
- `/16.14.1/champions` — Champs JSON-LD `ItemList.itemListElement[12].@type` : `ListItem` → (absent) (et 32 autre(s))
- `/16.14.1/objects` — Champs JSON-LD `ItemList.itemListElement[12].@type` : `ListItem` → (absent) (et 32 autre(s))
- `/champions?lang=fr_FR` — Champs JSON-LD `ItemList.itemListElement[12].@type` : `ListItem` → (absent) (et 32 autre(s))

### `item-list-index-urls`

**correction** — La prod écrit dans l'`ItemList` des objets l'indice de la carte au lieu de l'id (`/object/0`, `/object/1`…) : des liens morts. La cible pointe l'URL canonique de l'objet.

- `/objects` — Champs JSON-LD `ItemList.itemListElement[0].url` : `detail:items:0@latest` → `detail:items:1001@latest` (et 11 autre(s))
- `/16.14.1/objects` — Champs JSON-LD `ItemList.itemListElement[0].url` : `detail:items:0@16.14.1` → `detail:items:1001@16.14.1` (et 11 autre(s))

### `json-ld-html-escaping`

**correction** — La prod échappe en HTML le texte du JSON-LD (`&amp;`, `&#039;`) : les robots lisent « K&#039;Sante ». La cible écrit le texte brut, échappé pour le seul contexte du script.

- `/champion/Nunu` — Champs JSON-LD `@graph.@graph[2].name` : `Nunu &amp; Willump, LoL champion — League Of Data Base` → `Nunu & Willump, LoL champion — League Of Data Base`
- `/champion/KSante` — Champs JSON-LD `@graph.@graph[2].name` : `K&#039;Sante, LoL champion — League Of Data Base` → `K'Sante, LoL champion — League Of Data Base`
- `/champion/Belveth` — Champs JSON-LD `@graph.@graph[2].name` : `Bel&#039;Veth, LoL champion — League Of Data Base` → `Bel'Veth, LoL champion — League Of Data Base`

### `item-description-fallback`

**correction** — Pour un `plaintext` vide (objets LoL Classic), l'opérateur `??` de PHP garde la chaîne vide et la prod retire la description ; la cible retombe sur la description de Data Dragon.

- `/object/771004` — Champs JSON-LD `VideoGame.gameItem.description` : (absent) → `3 Mana Regen per 5 seconds`

### `prerendered-data-page`

**décision** — ADR 0005 : `/about/data` est prérendue au build, sans appel à l’API ; le patch courant n'y est donc ni dans la description, ni dans le nom ou la version du `Dataset` : la page le remplit dans le navigateur.

- `/about/data` — Description : `Where League Of Data Base data comes from: Riot's official Data Dragon export (patch 16.19…` → `Where League Of Data Base data comes from: Riot's official Data Dragon export and Communit…`
- `/about/data` — Champs JSON-LD `Dataset.description` : `Where League Of Data Base data comes from: Riot's official Data Dragon export (patch 16.19…` → `Where League Of Data Base data comes from: Riot's official Data Dragon export and Communit…` (et 2 autre(s))

### `dataset-languages`

**défaut** — `Dataset.inLanguage` liste les 21 locales du site au lieu des 28 langues de Data Dragon (la prod les écrivait au format `fr_FR`, hors BCP 47). Écart au « JSON-LD conservé à l'identique » de l'ADR 0005, à trancher par L3.10 (`features/editorial/about/about-data-page.ts`) : langues Data Dragon en BCP 47.

- `/about/data` — Champs JSON-LD `Dataset.inLanguage[0]` : `ar_AE` → `ar` (et 27 autre(s))

## Écarts non expliqués

Aucun.

## Hors échantillon

- `/trends`, `/developers`, `/donate` : pages provisoires des lots 5 et 6, sans SEO propre
  à ce stade ; leur diff revient à leurs chantiers.
- `/u/{username}` : les comptes n’existent que dans la base de la prod (répétition du lot 8).
- Pages de compte : jamais indexées (`noindex`), hors diff SEO.
- hreflang : la prod n’en a pas (ADR 0005) ; la suite E2E `specs/public/seo.spec.ts` vérifie
  les 21 locales et `x-default` sur chaque page publique.
