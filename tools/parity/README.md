# Parité PHP ↔ .NET des données Data Dragon (L1.8)

Collecte, sur les deux stacks, ce que compare `tests/LoDb.Parity` : la projection canonique
de chaque (version, langue) et les manifestes d'images. Node seul, sans dépendance.
Rapport : [`docs/reecriture/rapports/parite-lot-1.md`](../../docs/reecriture/rapports/parite-lot-1.md).

```bash
# 1. Stocker l'échantillon sur les deux stacks (écrivains : un seul à la fois par stack)
node tools/parity/collect.mjs --steps=warm,details --run=tools/parity/.runs/lot1
node tools/parity/collect.mjs --steps=ingest --run=tools/parity/.runs/lot1
# 2. Exporter les deux projections et copier les manifestes (lecture seule)
node tools/parity/collect.mjs --run=tools/parity/.runs/lot1
# 3. Comparer et classer ; écrit report.md et deviations.json(l) dans le run
LODB_PARITY_RUN=$PWD/tools/parity/.runs/lot1 \
  dotnet test --project tests/LoDb.Parity/LoDb.Parity.csproj
# Tests de l'outil
node --test 'tools/parity/test/*.test.mjs'
dotnet test --project tests/LoDb.Parity/LoDb.Parity.csproj   # sans run : paires construites
```

Options : `--versions=16.19.1,8.7.1` (remplace l'échantillon), `--langs=en_US,fr_FR`,
`--latest=10`, `--steps=` parmi `warm,details,ingest,export,manifests` (défaut
`export,manifests`), `--run=` (défaut `.runs/<horodatage>`, ignoré par Git).

## Échantillon

Les 10 dernières versions de `versions.json`, puis 8.7.1, 7.22.1, 7.21.1, 3.13.24 et
0.151.2, en `en_US`, `fr_FR`, `ko_KR`, `ar_AE` et `zh_CN`. Une langue que Data Dragon n'a
pas pour une version retombe sur `en_US` des deux côtés : N/A dans le rapport.

## Étapes

- `warm` : `app:ddragon:warmup --only=… --langs=…` dans le conteneur `php`, en
  `-u www-data`, avec `memory_limit=2G` (la limite de 256M ne tient pas l'échantillon
  complet en dev).
- `details` : visite des pages détail de quelques champions sur `localhost:8080`, seul
  chemin où l'ancienne stack stocke détail, icônes de sorts et chromas. Le résultat de
  chaque visite est dans `legacy/details.json`.
- `ingest` : `ingest --latest N` puis `ingest --version` des versions pièges, avec les
  langues de l'échantillon, sur `lodb-dev`. Une image sans verdict (503 transitoire)
  fait sortir en 1 : relancer `ingest --version` de la version citée.
- `export` : `php/` copié par `docker compose cp` dans le conteneur `php`, lancé en
  `-u www-data`, puis retiré ; les étiquettes de chromas sont dérivées par la règle du
  front historique (`legacy/app/assets/vue/chroma/chromaLabel.ts`, lue sans copie). Côté
  nouvelle stack, `catalog export --stored-only`.
- `manifests` : `manifest/{version}/` lu dans le volume `lodb_storage` monté en lecture
  seule par un conteneur jetable, et les lignes `ddragon_asset` de l'échantillon.

Aucun fichier de `legacy/app/` n'est modifié ; le script PHP n'écrit que dans `/tmp` du
conteneur, et ne lit que des datasets déjà stockés (`NotWarmedException` sinon, jamais
de récupération par go-fetcher).

## Run

```
sample.json
legacy/details.json                     visites de l'étape details
legacy/export/{version}/{langue}.json   projection PHP
legacy/manifest/{version}/{type}.json   manifestes de l'ancienne stack
next/export/{version}/{langue}.json     catalog export
next/assets.json                        lignes ddragon_asset
report.md, deviations.json(l)           écrits par tests/LoDb.Parity
```
