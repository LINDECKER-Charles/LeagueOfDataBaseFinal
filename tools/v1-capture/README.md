# Capture du contrat `/v1` de go-api

Enregistre les références de `tests/fixtures/v1/references/` en rejouant
`tests/fixtures/v1/scenarios/` sur go-api (chantier L6.1). Node seul, sans dépendance.

```bash
node tools/v1-capture/capture.mjs                  # capture et écrit les références
node tools/v1-capture/capture.mjs --check          # rejoue et compare, n'écrit rien
node tools/v1-capture/capture.mjs --only=01-auth   # groupes choisis (séparés par ,)
node tools/v1-capture/capture.mjs --refresh-schema [--schema-source=lodb-postgres-1]
node --test 'tools/v1-capture/test/*.test.mjs'     # tests de l'outil et des fixtures
```

`--check` sort en code 1 et liste les différences si un groupe ne reproduit pas sa
référence. Une capture complète dure environ 75 s (dont 61 s d'attente d'expiration du
cache de clés).

## Environnement, entièrement privé

- Image `lodb-v1-capture/go-api:<arbre>` construite depuis `legacy/go/api` (l'arbre Git est
  consigné dans chaque référence, champ `source.goApiTree`).
- Réseau `lodb-v1-capture`, Postgres `postgres:17-alpine` en mémoire (`--tmpfs`) sans port
  publié, chargé avec `seed/schema.sql` puis `seed/dataset.sql` avant chaque groupe.
- go-api publié sur `127.0.0.1:18990` (`--port` ou `V1_CAPTURE_PORT`), port hors de ceux
  de l'ancienne stack, de `lodb-dev` et de ses emplacements.
- Stockage généré dans un répertoire temporaire par groupe, monté dans le conteneur.
- Tout est supprimé en fin d'exécution, échec compris : conteneurs, réseau, répertoires.

La capture ne touche ni la base de dev, ni le volume `storage`, ni aucun conteneur de
l'ancienne stack. Seul `--refresh-schema` lit celle-ci : un `pg_dump --schema-only` du
conteneur Postgres de l'ancienne stack (projet `lodb`, démarré depuis la racine).

Prérequis : Docker, et Node 22 ou plus. Les agrégats sont datés depuis le jour UTC de la
capture : une capture qui chevauche minuit UTC peut donner des tendances décalées ;
il suffit de la relancer.
