# Outils de bascule (L8.2)

Scripts de la répétition locale (critère de sortie du lot 8) et de la fenêtre de bascule
([`plan-migration.md`](../../../docs/reecriture/plan-migration.md), « Bascule » et « Table
des 301 héritées »). Bash, Node sans dépendance, `curl`, `jq` et Docker. Le runbook qui les
enchaîne est celui de L8.3.

| Outil | Rôle | Étape du plan |
|---|---|---|
| `rehearse.sh` | répétition locale de bout en bout, retour arrière compris | 2 (en local) |
| `pre-ingest.sh` | pré-ingestion `ingest --latest 3 --languages all` | 3 |
| `smoke.sh` | smoke tests de la nouvelle stack, contre une URL quelconque | 4.5 |
| `readonly-e2e.sh` | E2E étiquetés `@readonly`, contre une URL quelconque | 4.5 |
| `check-301.mjs` | vérificateur des 301 à partir des anciens sitemaps ou d'une liste | 4.4 |
| `monitoring/queries.logsql` | requêtes LogsQL de surveillance (clés d'événement) | 4.6 |
| `monitoring/metrics.promql` | requêtes PromQL sur les métriques de l'API (port 9464 interne) | 4.6 |
| `monitoring/check-queries.sh` | exécute ces requêtes sur un VictoriaLogs jetable | — |
| `legacy-smoke.sh` | pages clés de l'ancien site après un retour arrière | 5 |
| `accounts.mjs` | comptes existants : connexion, clé d'API, puis ancien site | 2 et 5 |
| `compose.rehearsal.yaml` | surcouche : la nouvelle stack sur la copie de l'ancienne base | — |

Tests des bibliothèques (URLs héritées, sitemaps, verdicts) :

```bash
node --test 'tools/next/cutover/test/*.test.mjs'
```

## Répétition locale

```bash
npm ci --prefix tests/LoDb.E2E && npm --prefix tests/LoDb.E2E run browsers:install
tools/next/cutover/rehearse.sh --slot 2 --anonymize   # ancienne stack : legacy/ par défaut
```

Déroulé, chaque étape chronométrée dans un résumé final (code 0 si tout passe ; derrière un
`| tee`, lire ce code avec `set -o pipefail`, voir le [runbook](../../../docs/reecriture/bascule.md)
§ 3.1) :

1. **Copie** : `pg_dump` de la base de l'ancienne stack (projet `lodb`), passée au besoin par
   `tools/next/db/anonymize.sh` (`--anonymize`), restaurée dans une **base à part**
   (`--copy`, `lodb_rehearsal` par défaut) du même PostgreSQL. La base de l'ancienne stack
   n'est jamais écrite ; le script refuse une copie qui porterait son nom.
2. **Comptes existants** : un compte par format de hash de `tests/fixtures/hashes`
   (bcrypt `2y`/`2a`/coût 4, argon2i, argon2id sodium, faible, deux voies), écrits comme
   Doctrine, plus ceux de `--accounts-file` et, avec `--anonymize`, cinq comptes de la base
   qui ont un mot de passe. Une base de dev peut n'en avoir aucun : le script l'écrit sur
   stderr (`WARNING`) sans échouer, et les comptes semés tiennent lieu de comptes existants.
3. **`migrate`** deux fois dans l'emplacement (`-p lodb-next-e<n>`) : `Baseline` marquée,
   migrations additives appliquées, puis rien à appliquer.
4. **Pré-ingestion**, puis **nouvelle stack** démarrée sur la copie
   (`compose.rehearsal.yaml` : `api` et `migrate` rejoignent le PostgreSQL de l'ancienne
   stack par `host.docker.internal`, comme la prod rejoint la base existante).
5. **Contrôles** : `smoke.sh`, `check-301.mjs` sur les sitemaps de l'ancien site,
   `readonly-e2e.sh` (une reprise, comme en CI ; les tests repris sont listés `flaky`),
   `accounts.mjs new-stack` (connexion, hash réécrit en argon2id, clé
   d'API émise, `/v1/usage`).
6. **Retour arrière** : la nouvelle stack s'arrête, l'ancienne est relancée sur la copie
   migrée (`POSTGRES_DB` dans l'environnement de `docker compose`, sans toucher à son
   `.env`) ; `legacy-smoke.sh`, puis `accounts.mjs legacy-stack` : connexion par le
   formulaire Symfony avec le hash écrit par la nouvelle stack, profil, `/v1/usage` du go-api
   avec la clé émise par la nouvelle stack.
7. **Remise en état** : l'ancienne stack revient sur sa base (même en cas d'échec), puis
   l'emplacement est supprimé (`down -v --rmi local`) et la copie effacée, sauf `--keep`.
   `--stop-legacy` arrête l'ancienne stack à la fin.

Options : `--skip-pre-ingest`, `--skip-e2e`, `--no-build`, `--historical <n>` (sitemaps de
versions suivis par le vérificateur des 301). L'ancienne stack doit tourner depuis
`--legacy-dir` (`legacy/` de ce dépôt par défaut), ou y être arrêtée : si ses conteneurs
viennent d'un autre dossier (la racine du dépôt avant l'archivage, par exemple), le script
refuse de les recréer. Son `.env` est `legacy/.env`.

## Pré-ingestion

Avant la fenêtre, sur l'hôte servi, base déjà migrée :

```bash
tools/next/cutover/pre-ingest.sh --latest 3 -- -p <projet> -f compose.next.yaml -f compose.next.deploy.yaml
```

Le conteneur ponctuel `api` (`run --rm --no-deps`) remplit le volume `storage` et le
manifeste des trois dernières versions de Data Dragon dans toutes les langues. La longue
traîne reste à la demande. Un code 1 signale une version incomplète : une image refusée par
Data Dragon (503) ou une version tenue par la veille de patchs (`ingest.version.locked`).
Le script lance alors une seconde passe, une seule, qui la complète : les datasets et les
images déjà stockés sont gardés. Son code est celui de la dernière passe.
Pendant la répétition locale, la pré-ingestion précède le démarrage de l'API, donc de sa
veille.

## Vérificateur des 301

```bash
# Les sitemaps de l'ancien site, lus avant la bascule (ou enregistrés dans des fichiers)
node tools/next/cutover/check-301.mjs --base https://league-of-data-base.com \
  --sitemap old-sitemap.xml --legacy https://<ancien site encore servi> --historical 2
# Une liste d'URLs ou de chemins, une par ligne
node tools/next/cutover/check-301.mjs --base http://localhost:18280 --list urls.txt
```

Chaque ancienne URL doit répondre par **une** 301 vers sa nouvelle forme, puis un 200 à
l'arrivée. La nouvelle forme est vérifiée par sa structure (`lib/legacy-urls.mjs`) : locale
issue de `?lang=`, version épinglée ou retirée pour la dernière, segment de ressource, forme
de l'id (`{id}-{slug}` pour les items et les runes). `/` doit répondre 302 vers une locale,
les contrats (`/b/`, `/v1/`, `/webhooks/stripe`, `/cdn/blobs/`) ne sont jamais redirigés.
Une URL qu'aucune ligne de la table ne couvre est un échec. Le premier appel de chaque
sitemap part seul : une version froide est ingérée à la demande. Options : `--sample <n>`,
`--concurrency`, `--timeout-ms`, `--json <fichier>` (verdicts bruts).

Les sitemaps de l'ancien site sont à enregistrer **avant** la bascule : ensuite,
`/sitemap.xml` est celui de la nouvelle stack.

## Smoke tests et E2E en lecture seule

```bash
tools/next/cutover/smoke.sh https://league-of-data-base.com https://api.league-of-data-base.com
tools/next/cutover/readonly-e2e.sh https://league-of-data-base.com --workers=2
```

`@readonly` étiquette les tests qui n'écrivent rien : ni compte, ni build, ni vote, ni
message, ni tentative de connexion ; Stripe et les options de dons sont doublés dans la page.
Les pages visitées comptent tout de même comme des vues dans l'analytics, comme celles d'un
visiteur. Les tests qui écrivent (inscription, builds, portail des clés, admin, contact,
votes, profil) ne portent pas l'étiquette.

## Surveillance

`monitoring/queries.logsql` : une requête par question, avec les champs `{{stack}}`,
`{{domain}}` et `{{window}}`. Les requêtes portent sur des clés d'événement, jamais sur le
niveau deviné par Vector ([`observabilite.md`](../../../docs/guides/observabilite.md)) :

- 5xx et 4xx sur d'anciennes URLs dans le journal de l'edge ;
- statuts et échecs de rendu du serveur SSR ;
- erreurs de l'API par `EventName` ;
- anciennes URLs non résolues ;
- issues d'ingestion ;
- refus d'egress, tâches en échec, e-mails abandonnés, webhooks Stripe, `/v1` ;
- volume de lignes par service.

`monitoring/metrics.promql` donne les métriques d'ingestion, des tâches, des 5xx de l'API et
de l'outbox. Les noms sont ceux de l'exportateur Prometheus, relevés sur `/metrics` d'un
emplacement. Ces métriques ne sont visibles que si l'hôte collecte `api:9464` dans le réseau
de la stack ; ce port n'est jamais publié. Un compteur n'apparaît qu'à sa première
incrémentation.

```bash
tools/next/cutover/monitoring/check-queries.sh lodb-next-e2
```

Ce script charge les journaux d'un projet Compose local dans un VictoriaLogs jetable
(`_msg` = ligne brute, flux `stack` et `service`, comme Vector), puis exécute chaque requête.
Il prouve la syntaxe et le décompte sur les vraies lignes JSON. Les requêtes de l'edge n'y
trouvent rien, faute de journal d'edge en local.
