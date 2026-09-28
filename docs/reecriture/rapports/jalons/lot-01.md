# Jalon du lot 1 — données Data Dragon

- **Date** : 2026-09-26.
- **Branche** : `docs/reecriture-dotnet-angular`, sur `3898ea7`. Ce jalon a intégré L6.1
  (`wt/l6-1-references-v1`, fusion `2a712e6`), puis ajouté L1.8 : collecte `4ff9df8`,
  comparaison `e85cf2c` et `8390623`, rapport de parité `3898ea7`.
- **Poste** : macOS arm64, .NET SDK 10.0.400, Node 26.5 / npm 12.0.2, Docker 29.8.0.
- **Stacks** : `lodb-next` reconstruite depuis la racine sur `2a712e6` (07:03 UTC, 26 s,
  5 services `healthy`). L'ancienne stack (projet `lodb`) n'a servi qu'à la parité, puis a
  été arrêtée (`docker compose stop`, 6 conteneurs en `Exited (0)`).

Plan de référence : [plan maître §8](../../plan-implementation.md#8-jalons-et-critères-de-sortie) ;
critère du lot : [lot 1](../../implementation/lot-01-donnees-data-dragon.md). Livrables du
jalon : [`parite-lot-1.md`](../parite-lot-1.md) (L1.8) et
[`contrat-v1.md`](../contrat-v1.md) (L6.1, fusionné).

## 1. Commandes et résultats

Toutes les commandes sont lancées depuis la racine du dépôt.

| Étape | Commande | Résultat |
|---|---|---|
| Lockfiles | `npm install --prefix src/LoDb.Web` et `npm install --prefix tests/LoDb.E2E` | aucune dérive : rien à committer |
| Contrat, i18n | `npm --prefix src/LoDb.Web run api:generate` / `api:check` / `i18n:report` | bouchons, sortie 0 (L2.2, L3.3) ; aucun document OpenAPI n'est suivi par Git |
| Build .NET | `dotnet build LoDb.slnx -c Release` (puis `--no-incremental`) | 10 projets, 0 avertissement, 0 erreur |
| Tests .NET | `dotnet test LoDb.slnx` | 1 009 tests : 1 007 réussis, 2 ignorés (tests du run de parité, sans `LODB_PARITY_RUN`) |
| Parité | `LODB_PARITY_RUN=$PWD/tools/next/parity/.runs/lot1 dotnet test --project tests/LoDb.Parity/LoDb.Parity.csproj` | 50 réussis sur 50 ; 12 240 écarts, tous classés, aucun défaut de la nouvelle stack |
| Outil de parité | `node --test 'tools/next/parity/test/*.test.mjs'` | 6 réussis sur 6 |
| Outil de parité | `php -l` de `tools/next/parity/php/**` dans un conteneur jetable `php:8-cli-alpine` | aucune erreur de syntaxe |
| Front | `npm --prefix src/LoDb.Web run lint` | OK (9/9 tests des règles d'architecture) |
| Front | `npm --prefix src/LoDb.Web run typecheck` | OK |
| Front | `npm --prefix src/LoDb.Web run test` | 28 fichiers, 260 tests réussis |
| Front | `npm --prefix src/LoDb.Web run build:web` / `build:shell` | OK ; avertissement de budget sur `build:web` (§ 3) |
| Stack | `docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build` | 5 services en `healthy` |
| E2E | `npm --prefix tests/LoDb.E2E test` | 2 réussis sur 2 |
| E2E (mesure) | `npm --prefix tests/LoDb.E2E test -- --repeat-each=30` | 60 réussis sur 60 (8,8 s), lancé pour relever la mémoire |
| E2E | `npm --prefix tests/LoDb.E2E run typecheck` | OK |

Avertissements sans effet sur le résultat :

- `build:web` : budget initial de 549,20 ko, au-delà du seuil d'avertissement de 500 ko ;
  `DEP0205` sous Node 26, déjà connu au lot 0 ;
- `ingest` a rendu le code 1 sur 16.14.1, 16.11.1 et 8.7.1, chaque fois pour une image
  laissée sans verdict après des 503 de Data Dragon. `ingest --version` a réglé chacune en
  moins d'une seconde (détail dans [`parite-lot-1.md`](../parite-lot-1.md) § 2 et § 5).

Le dépôt n'a aucun hook de commit : rien à rejouer après la fusion de L6.1.

## 2. Échecs

Aucune suite n'échoue, et le critère du lot est vérifié (§ 5). Le jalon relève cependant
deux défauts, à corriger avant les lots qui en dépendent. Chacun forme un groupe. Les
groupes touchent des fichiers disjoints et peuvent donc être corrigés en parallèle.

### G1 — objet 7050 listé en `ar_AE` et `zh_CN` (défaut commun aux deux stacks)

**Reproduire** (stack démarrée, 16.19.1 ingérée) :

```bash
for l in en_US zh_CN ar_AE; do
  docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml \
    exec -T api dotnet LoDb.Api.dll catalog export --version 16.19.1 --lang "$l" \
    --Logging:LogLevel:Default=None --stored-only \
    | jq -c '.items.entries[] | select(.id == "7050") | {id, name, listed}'
done
```

**Sortie utile** :

```
{"id":"7050","name":"Gangplank Placeholder","listed":false}
{"id":"7050","name":"普朗克 占位","listed":true}
{"id":"7050","name":"نائب غانغ بلانك","listed":true}
```

**Cause**. `ItemDebris.IsDebris` repère un placeholder au mot `Placeholder` dans le nom
localisé. Or Data Dragon le traduit en `ar_AE` et en `zh_CN`, alors que le commentaire du
code affirme le contraire (« in every locale »). L'ancienne stack fait la même
erreur (`app/src/Service/API/ItemManager.php`, `paginationCollection`) : la parité ne voit
donc aucun écart, et les exports des deux côtés donnent `listed: true` dans ces deux
langues. L'objet apparaît alors dans la liste, la recherche, les comptes et le sitemap de
ces langues (UP 10).

**Fichiers suspects** :

- `src/LoDb.Domain/Derived/Items/ItemDebris.cs` ;
- `src/LoDb.Ingestion/Catalog/Snapshots/CatalogSnapshot.cs` (`ListedItems`, qui dispose
  déjà du catalogue `en_US` par `englishItemNames`) ;
- `src/LoDb.Ingestion/Catalog/Export/ExportProjection.cs` (champ `listed`) ;
- `tests/LoDb.Domain.Tests/Derived/Items/ItemDebrisTests.cs` et les tests du snapshot ;
- `tests/LoDb.Parity/Classification/ParityRules.cs`, `tests/LoDb.Parity/Tests/ParityRulesTests.cs`
  et `docs/reecriture/rapports/parite-lot-1.md`, pour la règle ajoutée.

**Correction proposée** : décider du statut de débris d'un objet sur son nom `en_US` (même
id, même version), comme le type (heritage § 5.13), et garder le test du nom vide sur le
nom localisé. Une fois l'objet retiré de la liste dans ces deux langues, la parité fera
apparaître un écart `listed` sur 7050. Il faudra lui ajouter une règle
`LegacyDefect` (« placeholder traduit »), testée sur son cas et sur le cas le plus
proche, puis rejouer la comparaison du § 1 : le run `lot1` suffit, sans recollecte.
Vérification : la reproduction ci-dessus doit sortir `listed:false` dans les trois
langues.

### G2 — mémoire de l'API après une ingestion complète, à 15 Mio de la limite

**Reproduire** (stack démarrée depuis au moins une ingestion toutes langues) :

```bash
docker stats --no-stream --format '{{.Name}} {{.MemUsage}}' lodb-next-api-1
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml \
  exec -T nginx wget -qO- http://api:9464/metrics \
  | grep -E '^dotnet_(gc_last_collection_heap_size|process_memory_working_set)'
```

**Sortie utile** (relevé de fin de jalon) :

```
lodb-next-api-1 372.8MiB / 11.67GiB
dotnet_gc_last_collection_heap_size_bytes{…,gc_heap_generation="gen2"} 13634480
dotnet_gc_last_collection_heap_size_bytes{…,gc_heap_generation="loh"} 69400520
dotnet_process_memory_working_set_bytes{otel_scope_name="System.Runtime"} 326815744
```

**Constat**. Après l'ingestion de 16.19.1 en 28 langues par la veille, puis 14 `ingest` et
75 `catalog export` de la parité, l'API occupe 369 à 373 Mio. `compose.next.deploy.yaml`
la limite à 384m. Le tas des gros objets retient 66 Mio après la dernière collecte. Le
reste du working set est natif (SkiaSharp, runtime). En dev, sans limite, le GC n'a
aucune raison de rendre la mémoire : ce relevé ne prouve donc pas un dépassement, mais il
ne prouve pas non plus que l'ingestion tient sous 384m. Relevés complets :
[`memoire.md`](../memoire.md), « Lot 1 — jalon ».

**Fichiers suspects** :

- `src/LoDb.Ingestion/Images/ImageIngestion.cs` et `WebpTranscoder.cs` (tampons d'images
  et de WebP, candidats au LOH) ;
- `compose.next.deploy.yaml` (limite `api`, et éventuel `DOTNET_GCHeapHardLimit`).

**Correction proposée** : mesurer d'abord dans un emplacement `lodb-next-e1`, lancé
depuis un worktree avec la limite de 384m : une ingestion toutes langues d'une version
neuve, puis `ingest --latest 10`, en relevant le pic et les éventuels `OOMKilled`. Selon
le résultat, deux pistes. Soit réduire la rétention : tampons loués ou flux au lieu de
`byte[]` entiers, lot d'images borné. Soit ajuster la limite, ou fixer un
`GCHeapHardLimit` cohérent avec elle. Supprimer l'emplacement (`down -v`) avant de rendre.
Seuil à tenir avant L8.1 : aucun `OOMKilled` sous 384m, et un pic consigné dans
`memoire.md`.

## 3. Points ouverts, sans échec

Aucun de ces points n'empêche le critère. Ils sont à trancher, ou à traiter par le
chantier cité.

- **Budget du bundle `web`** : 549 ko contre 500 ko d'avertissement. Il faut le suivre au
  lot 3 (découpage paresseux des routes) avant qu'il n'atteigne le seuil d'erreur.
- **Reprise automatique des 503 transitoires**. `ingest` rend le code 1 et ne persiste
  rien, comme le veut le plan. Le jalon n'a pas observé la veille reprendre seule une
  version restée incomplète, parce que les trois reprises ont été faites à la main. À
  vérifier par un test de la veille si ce n'est pas déjà couvert.
- **Warmup de l'ancienne stack** : 256 Mo ne suffisent pas en dev sur l'échantillon de
  parité. Ce comportement de l'ancienne stack est hors périmètre et ne doit pas être
  corrigé. L'outil passe `-d memory_limit=2G`, et le piège figure dans `CLAUDE.md`.
- **Taille de `CLAUDE.md`** : 418 lignes avec les pièges du lot 1. Il faudra le résumer ou
  déplacer une partie des pièges dans les guides avant qu'il ne grossisse encore.
- **Point G3 du lot 0** (`autoCsp` dans le plan) : toujours ouvert, hors périmètre de ce
  jalon, à corriger avant L3.11.

## 4. Mémoire

Les relevés du jalon (API après ingestion, pics des E2E) sont consignés dans
[`../memoire.md`](../memoire.md), section « Lot 1 — jalon ». G2 en découle.

## 5. Critère de sortie local du lot 1

| Volet | État | Preuve |
|---|---|---|
| Suites vertes | **vérifié** | § 1 : .NET 1 007/1 007 hors 2 ignorés, front complet, E2E 2/2 |
| Parité des datasets et manifestes, 10 dernières versions × 5 langues + versions pièges | **vérifié** | [`parite-lot-1.md`](../parite-lot-1.md) : 16.19.1 à 16.10.1, puis 8.7.1, 7.22.1, 7.21.1, 3.13.24 et 0.151.2, en `en_US`, `fr_FR`, `ko_KR`, `ar_AE` et `zh_CN` ; 12 240 écarts, 0 non classé, 0 défaut de la nouvelle stack ; `ar_AE` en N/A sur les 5 versions pièges (retombée `en_US` des deux côtés) |
| Un patch complet ingéré sans intervention | **vérifié** | 16.19.1, découverte par la veille à 07:03:32 UTC (`ingest.version.discovered`), puis « Ingested version 16.19.1 in 64494 ms: 112 datasets and 2009 images written, promoted True » (`ingest.version.completed`) ; `ddragon_version` 16.19.1 en `ready`, promue |
| Toutes les langues, images des quatre ressources | **vérifié** | `data/16.19.1/` : 28 langues × 4 datasets ; `ddragon_asset` 16.19.1 : champion 1 038, item 870, runesReforged 67, summoner 34, toutes `present` |
| WebP produit dans le conteneur | **vérifié** | 1 750 blobs distincts pour 16.19.1, chacun avec son jumeau `.webp` (0 manquant, volume `lodb-next_storage` lu en lecture seule) ; en-tête `RIFF…WEBP` |

**Critère du lot 1 : vérifié.** G1 et G2 ne bloquent pas le critère. G1 est à corriger
avant que le lot 3 ne serve la liste des objets en `ar_AE` et `zh_CN`. G2 doit être mesuré
avant L8.1.

## 6. Vérification

- **Date** : 2026-09-26, 07:55 à 08:05 UTC.
- **Branche** : `docs/reecriture-dotnet-angular`. Une seule branche de correction :
  `wt/corr-l1-g1-objet-7050` (dernier commit `2049279`), fusionnée sans conflit en
  `e80af38`. Le décompte de parité est reporté en `8a1caf5`. G2 n'a pas de branche.
- **Stacks** : `lodb-next` reconstruite depuis la racine sur `e80af38` (17 s, 5 services
  `healthy`, `api` recréée). L'ancienne stack a été démarrée depuis la racine pour le
  ré-export, puis arrêtée (`docker compose stop`, 6 conteneurs en `Exited (0)`).

### Commandes et résultats

| Étape | Commande | Résultat |
|---|---|---|
| Lockfiles | `npm install --prefix src/LoDb.Web` et `npm install --prefix tests/LoDb.E2E` | aucune dépendance modifiée par la correction, aucune dérive |
| Build .NET | `dotnet build LoDb.slnx -c Release` (puis `--no-incremental`) | 0 avertissement, 0 erreur |
| Tests .NET | `dotnet test LoDb.slnx` | 1 023 tests : 1 021 réussis, 2 ignorés (run de parité, sans `LODB_PARITY_RUN`) |
| Front | `npm --prefix src/LoDb.Web run lint` / `typecheck` | OK (9/9 règles d'architecture) / OK |
| Front | `npm --prefix src/LoDb.Web run test` | 28 fichiers, 260 tests réussis |
| Front | `npm --prefix src/LoDb.Web run build:web` / `build:shell` | OK ; même avertissement de budget (549,20 ko, § 3) |
| Stack | `docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build` | 5 services en `healthy` |
| E2E | `npm --prefix tests/LoDb.E2E test` / `run typecheck` | 2 réussis sur 2 / OK |
| Parité, ré-export | `node tools/next/parity/collect.mjs --steps=export --run=tools/next/parity/.runs/lot1 --versions=<les 15 du run> --langs=en_US,fr_FR,ko_KR,ar_AE,zh_CN` | 66 s, 75 exports par côté ; run d'avant conservé sous `.runs/lot1-jalon` (ignoré par Git) |
| Parité, comparaison | `LODB_PARITY_RUN=$PWD/tools/next/parity/.runs/lot1 dotnet test --project tests/LoDb.Parity/LoDb.Parity.csproj` | 55 réussis sur 55 ; 12 260 écarts, 0 non classé, 0 défaut de la nouvelle stack |

Le dépôt n'a aucun hook de commit : rien à rejouer après la fusion.

### Échecs du jalon

**G1 — corrigé.** La reproduction du § 2, rejouée sur `lodb-next` reconstruite, sort :

```
{"id":"7050","name":"Gangplank Placeholder","listed":false}
{"id":"7050","name":"普朗克 占位","listed":false}
{"id":"7050","name":"نائب غانغ بلانك","listed":false}
```

Dans la parité, l'écart attendu apparaît : 20 écarts `listed` `true` → `false`, sur 7050
en `ar_AE` et en `zh_CN`, de 16.10.1 à 16.19.1. Ils sont tous classés sous la nouvelle
règle `legacy-translated-placeholder-listed` (`LegacyDefect`). Les autres groupes gardent
leur décompte : le total passe de 12 240 à 12 260, `LegacyDefect` de 308 à 328, et
`Expected` reste à 11 932. Détail : [`parite-lot-1.md`](../parite-lot-1.md) § 4.

**G2 — persistant, non traité.** Aucune branche de correction. La mesure sous 384m exige un
emplacement `lodb-next-e1` et une décision sur la limite ou sur `GCHeapHardLimit`. Ni l'une
ni l'autre n'a été faite. Relevé de la vérification, sur l'API juste recréée, donc sans
valeur probante sur le pic :

```
lodb-next-api-1 62.02MiB / 11.67GiB
dotnet_gc_last_collection_heap_size_bytes{…,gc_heap_generation="loh"} 6443184
dotnet_process_memory_working_set_bytes{otel_scope_name="System.Runtime"} 141000704
```

Le seuil du § 2 reste à tenir avant L8.1 : aucun `OOMKilled` sous 384m, et un pic consigné
dans [`memoire.md`](../memoire.md).

### Critère de sortie, revérifié

La correction ne touche que la lecture du catalogue (`CatalogSnapshot`, export, recherche).
L'ingestion n'est pas modifiée (`ItemDebris` n'est appelé que depuis le catalogue) : les
volets « patch ingéré sans intervention », « toutes les langues » et « WebP » gardent la
preuve du § 5 sans nouvelle ingestion.

| Volet | État | Preuve |
|---|---|---|
| Suites vertes | **vérifié** | tableau ci-dessus : .NET 1 021/1 021 hors 2 ignorés, front complet, E2E 2/2 |
| Parité, 75 couples version × langue | **vérifié** | 12 260 écarts, 0 non classé, 0 défaut de la nouvelle stack ; les couples touchés par G1 (`ar_AE`, `zh_CN` × 16.10.1–16.19.1) sont ré-exportés et comparés |
| Patch complet, langues, images, WebP | **vérifié** (§ 5, inchangé) | pipeline d'ingestion non modifié par la correction |

**Critère du lot 1 : vérifié.** G1 est corrigé. G2 reste ouvert, sans effet sur le
critère, et doit être mesuré avant L8.1.
