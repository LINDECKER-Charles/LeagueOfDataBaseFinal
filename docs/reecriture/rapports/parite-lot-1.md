# Parité PHP ↔ .NET des données Data Dragon — lot 1 (L1.8)

- **Date** : 2026-09-26, run `lot1` collecté à 07:23 UTC, puis ré-exporté à 07:59 UTC par la
  vérification du lot, après la correction G1 (fusion `e80af38`).
- **Branche** : `docs/reecriture-dotnet-angular`. La stack `lodb-next` a été reconstruite
  sur `2a712e6` ; l'outil est en `4ff9df8` (collecte) et `e85cf2c` (comparaison).
- **Poste** : macOS arm64, .NET SDK 10.0.400, Node 26.5, Docker 29.8.0.
- **Stacks** : l'ancienne stack (projet `lodb`, ports 8080…), puis `lodb-next`. L'ancienne
  stack a été arrêtée (`docker compose stop`) une fois la collecte terminée.

Critère visé ([lot 1](../implementation/lot-01-donnees-data-dragon.md#l18--parité-php--net)) :
parité des datasets et manifestes normalisés sur les 10 dernières versions × 5 langues,
plus les versions pièges, avec un rapport sans écart non classé.

**Résultat** : 12 260 écarts, tous classés. Aucun n'est un défaut de la nouvelle stack.
Trois règles couvrent 328 défauts de l'ancienne stack que la nouvelle corrige
volontairement ; trois règles couvrent 11 932 écarts attendus, tous dus au stockage
paresseux de l'ancienne stack.

## 1. Méthode

Outil : [`tools/next/parity/`](../../../tools/next/parity/README.md) pour la collecte,
[`tests/LoDb.Parity/`](../../../tests/LoDb.Parity) pour la comparaison et le classement.

- **Échantillon** : 16.19.1 à 16.10.1 (les 10 dernières versions de `versions.json`),
  puis 8.7.1, 7.22.1, 7.21.1, 3.13.24 et 0.151.2, en `en_US`, `fr_FR`, `ko_KR`, `ar_AE`
  et `zh_CN`, soit 75 couples (version, langue).
- **Projection canonique**. Côté ancienne stack, un script PHP en lecture seule passe par
  les managers de l'application. Côté nouvelle stack, `catalog export --stored-only`.
  Les champs comparés : ids, clés, noms, titres, édition et jumeau (objets et sorts
  Classic compris), listage, tier et lignes de stats des objets, jeton de ressource et
  classe de portée des champions, arbres et emplacements de runes, et le verdict de chaque
  image (`present` avec son blob, `absent`, `pending`). Pour les champions dont l'ancienne
  stack détient le détail, s'y ajoutent passif, sorts, skins (numéro compris) et chromas
  (étiquette dérivée des deux côtés : la règle du front historique `chromaLabel.ts` contre
  `ChromaLabel`).
- **Manifestes** : `manifest/{version}/{type}.json` du volume `lodb_storage`, lu en lecture
  seule par un conteneur jetable, contre les lignes `ddragon_asset`. La comparaison se fait
  clé par clé : même SHA-256 et même extension, ou absence des deux côtés.
- **Non comparés, par construction** :
  - le chemin canonique (`path`), nouveau schéma d'URL de l'[ADR 0005](../adr/0005-web-ssr-urls-et-seo.md)
    sans équivalent dans l'ancienne stack ;
  - le détail des champions que l'ancienne stack n'a jamais stocké, puisqu'elle ne le
    récupère qu'à la visite d'une page. L'échantillon visite 7 champions par couple (§ 2).

Les numériques se comparent par valeur : PHP écrit `25.0` et .NET `25` pour le même
flottant. Les listes à identité (`id`, ou `stat` pour les lignes de stats) se comparent
élément par élément, puis sur l'ordre. Un id répété (les skins sans id de 0.151.2) est
apparié par rang.

## 2. Déroulé

| Étape | Commande | Résultat |
|---|---|---|
| Reconstruction | `docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build` | 26 s ; api, web-ssr, nginx, postgres, mailpit `healthy` |
| Warmup, 1er essai | `docker compose exec -T -u www-data php php bin/console app:ddragon:warmup --only=<15 versions> --langs=<5 langues>` | **échec**, code 255 après 7 min 57 : mémoire PHP épuisée (256 Mo) sur `ItemManager` 8.7.1 `zh_CN` (§ 5) |
| Warmup | même commande, `php -d memory_limit=2G` | 141,8 s, `OK=300, erreurs=0` |
| Pages détail | `node tools/next/parity/collect.mjs --steps=details --run=…/lot1` | 525 visites en 71 s : 425 pages détail en 200, 95 renvois vers la liste (champion absent de la version), 5 × 404 (`FiddleSticks` sans version épinglée) |
| Ingestion | `ingest --latest 10 --languages en_US,fr_FR,ko_KR,ar_AE,zh_CN` | 549 s, **code 1** : 3 images sans verdict après quatre 503 de Data Dragon (§ 5) |
| Ingestion | `ingest --version <v> --languages …` pour chaque version piège | 33 à 44 s chacune ; 8.7.1 en code 1 (même cause), les autres en 0 |
| Reprise | `ingest --version <v> --languages …` pour 16.14.1, 16.11.1 et 8.7.1 | code 0, 1 image réglée par version (`LissandraQ.png`, `1111.png`, `EkkoQ.png`) |
| Collecte | `node tools/next/parity/collect.mjs --run=tools/next/parity/.runs/lot1` | 68 s : 75 exports par côté, 15 répertoires de manifestes, lignes `ddragon_asset` |
| Comparaison | `LODB_PARITY_RUN=$PWD/tools/next/parity/.runs/lot1 dotnet test --project tests/LoDb.Parity/LoDb.Parity.csproj` | 50 tests réussis sur 50 (dont les 2 du run) ; 12 240 écarts |
| Ré-export (vérification) | `node tools/next/parity/collect.mjs --steps=export --run=tools/next/parity/.runs/lot1 --versions=<15 versions> --langs=<5 langues>` | 66 s : 75 exports par côté, `lodb-next` reconstruite sur `e80af38` |
| Comparaison (vérification) | même commande | 55 tests réussis sur 55 ; 12 260 écarts, 0 non classé |

Avant `details`, 16.19.1 avait déjà été ingérée par la veille de patchs de `lodb-next`, en
28 langues et sans intervention. `ingest --latest 10` l'a donc trouvée prête.

## 3. Couverture

| Ressource | Entrées, ancienne stack | Entrées, nouvelle stack |
|---|---:|---:|
| champions | 12 130 | 12 130 |
| items | 45 220 | 45 220 |
| runes (arbres) | 300 | 300 |
| summoners | 1 730 | 1 730 |

Détails de champions comparés : 426, dont 401 non vides (1 604 sorts, 5 383 skins,
13 844 chromas). Les 25 vides sont couverts par la règle
`legacy-empty-detail-in-fallback-language`.

| Manifeste | Clés, ancienne stack | Clés, nouvelle stack | Communes |
|---|---:|---:|---:|
| champion | 2 856 | 14 548 | 2 856 |
| item | 9 004 | 9 044 | 9 004 |
| runesReforged | 801 | 801 | 801 |
| summoner | 346 | 346 | 346 |

Toutes les clés communes portent le même verdict : même blob, ou absence des deux côtés.
Par exemple, les icônes `.dds` des runes de 7.22.1 à 8.7.1 sont absentes des deux côtés.

**Langues**. N/A : Data Dragon n'a pas la langue pour cette version, et les deux stacks
retombent sur `en_US`, à l'identique. Les autres couples sont comparés dans leur langue.

| Version | en_US | fr_FR | ko_KR | ar_AE | zh_CN |
|---|---|---|---|---|---|
| 16.19.1 … 16.10.1 | ✓ | ✓ | ✓ | ✓ | ✓ |
| 8.7.1 | ✓ | ✓ | ✓ | N/A | ✓ |
| 7.22.1 | ✓ | ✓ | ✓ | N/A | ✓ |
| 7.21.1 | ✓ | ✓ | ✓ | N/A | ✓ |
| 3.13.24 | ✓ | ✓ | ✓ | N/A | ✓ |
| 0.151.2 | ✓ | ✓ | ✓ | N/A | ✓ |

## 4. Écarts classés

| Règle | Classe | Écarts | Versions |
|---|---|---:|---:|
| `legacy-ability-icons-on-visit` | attendu | 11 692 | 15 |
| `legacy-empty-detail-in-fallback-language` | défaut ancien corrigé | 298 | 5 |
| `legacy-image-not-fetched` | attendu | 200 | 11 |
| `legacy-debris-images-not-warmed` | attendu | 40 | 11 |
| `name-trimmed` | défaut ancien corrigé | 10 | 10 |
| `legacy-translated-placeholder-listed` | défaut ancien corrigé | 20 | 10 |
| *non classés* | — | 0 | — |
| *défauts de la nouvelle stack* | — | 0 | — |

Chaque règle est testée sur l'écart qu'elle couvre et sur l'écart le plus proche qu'elle
doit laisser passer (`ParityRulesTests`). Toutes les règles sont dans
[`ParityRules.cs`](../../../tests/LoDb.Parity/Classification/ParityRules.cs).

### `legacy-ability-icons-on-visit` — attendu

Clés `manifest/champion` présentes seulement dans la nouvelle stack, étiquetées icône de
passif ou de sort, hors champions visités. Exemple : 16.19.1 `AkaliE.png`, `∅` →
`present 60e23d…c0a6.png`.

L'ancienne stack ne stocke ces icônes qu'à l'affichage d'une page détail
(`ChampionController::champion`) ; son warmup ne prend que les portraits. La nouvelle
ingère toutes les images d'une version (L1.6). La règle ne couvre ni les portraits, ni
les icônes des champions visités. Celles-ci sont comparées : 100 % identiques.

### `legacy-empty-detail-in-fallback-language` — défaut de l'ancienne stack, corrigé

`ar_AE` sur 8.7.1, 7.22.1, 7.21.1, 3.13.24 et 0.151.2, champions visités. Dans l'ancienne
stack, passif `null`, aucun sort, aucun skin. Dans la nouvelle, ceux de `en_US` : 25
passifs, 100 sorts et 173 skins.

Pour une langue absente d'une version, l'ancienne stack retombe sur `en_US` pour les
datasets. Elle stocke pourtant un détail vide : `data/8.7.1/ar_AE/championDetail/Aatrox.json`
contient `[]`. Sa page détail perd alors passif, sorts et skins, alors que la liste du même
couple est affichée en anglais. La nouvelle stack applique le repli `en_US` au détail comme
aux datasets ([heritage](../heritage.md) § 5.2 et § 6, « Absences »).

### `legacy-image-not-fetched` — attendu

Images `pending` dans la projection de l'ancienne stack et `present` dans la nouvelle :
les objets 2008, 7050, 226660, 772139, 772140 et 3632 (7.21.1).

L'ancienne stack ne récupère que les images des entrées listées dans la première langue du
warmup (`en_US`). Toutes les autres attendent une page qui les demande. `pending` n'est pas
un verdict : le contenu est comparé par les manifestes, sous la règle suivante.

La règle couvre aussi 7050 en `ar_AE` et `zh_CN`, que l'ancienne stack liste dans ces deux
langues. En `en_US`, elle ne le liste pas, et le warmup ne l'a donc pas pris (voir § 5, premier point).

### `legacy-debris-images-not-warmed` — attendu

Les mêmes images, côté manifeste, présentes seulement dans la nouvelle stack. Leur clé est
étiquetée débris (non listé), dans au moins une langue de la version.

Le warmup de l'ancienne stack saute les objets écartés par sa collection parcourable. La
nouvelle stack ingère leur image comme celle de tout objet : les recettes les lisent
encore ([heritage](../heritage.md) § 5.10).

### `name-trimmed` — défaut de l'ancienne stack, corrigé

Objet 1520, `zh_CN`, 16.10.1 à 16.19.1 : `"过载 - 嚎哭深渊 "` → `"过载 - 嚎哭深渊"`.

Data Dragon livre ce nom avec une espace finale. `DdragonText.PlainName` épure tout nom
affiché. L'ancienne stack ne réduit que les noms balisés : elle garde l'espace dans le nom
qu'elle affiche. La règle n'accepte que des noms égaux une fois les blancs de bord
retirés.

### `legacy-translated-placeholder-listed` — défaut de l'ancienne stack, corrigé

Objet 7050, champ `listed`, `ar_AE` et `zh_CN` : `true` → `false`. Règle ajoutée par la
correction G1 du [jalon du lot 1](jalons/lot-01.md), et mesurée par la vérification du
lot sur le run `lot1` ré-exporté : 20 écarts, 7050 en `ar_AE` et en `zh_CN` de 16.10.1 à
16.19.1. C'est le décompte attendu : 7050 porte `Placeholder` dans son nom `en_US` sur ces
10 versions, et seuls `ar_AE` et `zh_CN` le traduisent (`fr_FR` et `ko_KR` gardent le mot).
Les autres règles gardent leur décompte.

Data Dragon traduit le mot `Placeholder` du nom en `ar_AE` (`نائب غانغ بلانك`) et en
`zh_CN` (`普朗克 占位`). L'ancienne stack le cherche dans le nom traduit
(`ItemManager::paginationCollection`) et liste donc l'objet dans ces langues. La nouvelle
stack le cherche dans le nom `en_US` du même id et de la même version, comme pour le type
([heritage](../heritage.md) § 5.13, UP 10). Le test du nom vide reste fait sur le nom
localisé.

La règle est étroite. Elle ne vise que le champ `listed` d'un objet, `true` dans
l'ancienne stack et `false` dans la nouvelle, et exige l'étiquette `placeholder`. Le run
pose cette étiquette sur un objet dont le nom, dans l'export `en_US` de la nouvelle stack
pour la même version, contient `Placeholder`. Elle ne couvre donc pas un objet que la
nouvelle stack cacherait pour une autre raison (un nom fait de jetons, par exemple), ni un
placeholder qu'elle listerait (`false` → `true`). Ces deux cas sont testés
(`ParityRulesTests`). Sans export `en_US` dans le run, aucune entrée n'est étiquetée et
l'écart reste non classé.

## 5. Observations hors écart

- **Défaut commun, 7050 « Gangplank Placeholder »**. Les deux stacks écartent les
  « placeholders » par le mot `Placeholder` dans le nom. Or ce nom est traduit en `ar_AE`
  (`نائب غانغ بلانك`) et en `zh_CN` (`普朗克 占位`). L'objet reste donc listé dans ces
  langues, des deux côtés, à l'encontre de ce que disent les deux sources (« in every
  locale »). Au moment de la mesure, ce n'est pas un écart de parité. La nouvelle stack
  le décide désormais sur le nom `en_US` de l'id, comme le type (heritage § 5.13) :
  correction G1 du jalon du lot 1. L'écart qui en résulte relève de la règle
  `legacy-translated-placeholder-listed` (§ 4).
- **503 transitoires de Data Dragon**. Sur 3 des 15 versions, une image sur environ 2 000
  n'a pas eu de verdict après quatre essais en 5 s environ. L'ingestion l'a laissée sans verdict,
  n'a rien persisté, et `ingest` a rendu le code 1, comme prévu. Rejouée, chacune a été
  réglée en 1 s, et les trois URL répondaient 200 entre-temps. Le comportement est
  conforme au plan (échec transitoire jamais persisté) ; la reprise automatique relève de
  la veille.
- **Mémoire du warmup de l'ancienne stack**. En `dev`, `app:ddragon:warmup` sur
  l'échantillon complet dépasse 256 Mo. La pile d'erreur passe par `TraceableAdapter` et
  `ApcuAdapter` : c'est probablement la trace du cache en debug qui s'accumule. L'outil
  de parité passe donc `-d memory_limit=2G`.

## 6. Rejouer

```bash
node tools/next/parity/collect.mjs --steps=warm,details --run=tools/next/parity/.runs/lot1
node tools/next/parity/collect.mjs --steps=ingest --run=tools/next/parity/.runs/lot1
node tools/next/parity/collect.mjs --run=tools/next/parity/.runs/lot1
LODB_PARITY_RUN=$PWD/tools/next/parity/.runs/lot1 \
  dotnet test --project tests/LoDb.Parity/LoDb.Parity.csproj
```

Le run écrit `report.md` (tableaux de ce rapport), `deviations.json` (groupes et exemples)
et `deviations.jsonl` (chaque écart avec sa règle). Il faut rejouer `ingest --version` pour
toute version citée par un `ingest` en code 1.
