# 🔐 GitHub Actions — pipeline de la nouvelle stack

Ce guide décrit le pipeline : qui construit, qui déploie, dans quel ordre. **La liste de
tout ce qu'il faut configurer** (environnements GitHub, secrets, variables, lignes des
`.env`, fichiers de l'hôte, services externes) est dans [`configuration.md`](configuration.md).

Les workflows de l'ancienne stack (`ci.yml`, `_*.yml`) sont archivés sous
`legacy/.github/workflows/` et ne tournent plus ; leur guide est
[`legacy/docs/guides/github-actions-secrets.md`](../../legacy/docs/guides/github-actions-secrets.md).

## Workflows

| Fichier | Rôle |
|---|---|
| `ci.yml` | Déclenché par `push` (`dev`, `main`, `docs/reecriture-dotnet-angular`) et `pull_request`, filtré sur les chemins de la nouvelle stack. Jobs parallèles : `dotnet` (build + tests, Testcontainers), `front` (lint, typecheck, tests, `build:web`, `build:shell`), `contract` (`api:check`), `i18n` (`i18n:report`, non bloquant), `e2e` (stack `lodb-preprod` + Playwright). Sur un `push` de **`dev`**, une fois les jobs bloquants verts : `build`, puis `deploy-preprod`. |
| `build.yml` | Réutilisable, appelé par `ci.yml` : images `ghcr.io/<owner>/lodb/{api,web-ssr,nginx}` taguées `:<sha>` + `:preprod`, label OCI et `APP_REVISION` = SHA. |
| `deploy.yml` | Déploiement SSH. Pour `preprod` : appelé par `ci.yml` après chaque build de `dev`, ou lancé à la main (`workflow_dispatch`, entrée `branch`, `dev` par défaut). Pour la prod : appelé par `promote.yml`. `pull`, puis conteneur éphémère `migrate` **avant** `up -d --wait`, puis smoke test (nginx, `/readyz`, `/en/`, sous-domaine `api.`, `X-Robots-Tag`, TLS public avec relance de l'edge). |
| `promote.yml` | Manuel (`workflow_dispatch`, entrées `revision`, `branch`, `take_over_domains`) : vérifie que les trois images `:<revision>` existent, puis, **après approbation** de l'environnement `production`, les retague `:production` (sans rebuild) et les déploie par `deploy.yml`. |
| `release-desktop.yml`, `release-android.yml` | Releases signées des apps ([`release-desktop.md`](release-desktop.md), [`release-android.md`](release-android.md)). |

```
push dev ─▶ ci (dotnet, front, contract, e2e ; i18n informatif)
         ─▶ build (GHCR :<sha> + :preprod)
         ─▶ deploy-preprod ─▶ hôte preprod : pull ─▶ migrate ─▶ up -d ─▶ smoke test
                          (annonce « Deployed revision : <sha> »)
Actions ▸ Promote (revision = <sha>) ─▶ images :<sha> présentes ?
        ─▶ approbation « production » ─▶ retag :<sha> → :production
        ─▶ hôte prod : pull ─▶ migrate ─▶ [reprise des domaines] ─▶ up -d ─▶ smoke test
```

Sur `dev`, un push plus récent **attend** la fin du run en cours au lieu de l'annuler : ce
run se termine par un déploiement, qu'une annulation couperait en plein milieu. Ailleurs
(PR, autres branches), le run obsolète est annulé.

Les jobs de CI et de build n'utilisent aucun secret (seulement `GITHUB_TOKEN`). Le job de
déploiement tourne dans l'environnement GitHub `preprod` ou `production` :

- **`preprod`** : créé à la première exécution ; ses deux secrets (`ENV_FILE`,
  `DATA_PROTECTION_PFX`) y sont déclarés. Tant qu'ils manquent, le job `deploy-preprod`
  échoue à son étape « Check the secrets », qui les nomme : le push de `dev` est alors rouge.
- **`production`** : à créer **avant** la première promotion, avec des *required
  reviewers* (la validation manuelle qui met en prod) et une règle *deployment branches*
  limitée à `dev`, la branche qui porte les workflows lancés. Ses deux secrets
  (`ENV_FILE`, `DATA_PROTECTION_PFX`) y sont déclarés, jamais en secrets de
  dépôt : ils ne sont alors délivrés qu'au job approuvé.

Le VPS étant commun aux deux stacks, la connexion réutilise les secrets de dépôt de
l'ancienne : `PROD_SSH_KEY`, `PROD_HOST`, `PROD_SSH_USER`. Le job déploie dans `/opt/lodb-preprod` et
`/opt/lodb-production` ([`configuration.md`](configuration.md), § 3.2).

**Tags d'images** : `:<sha>` (immuable, poussé par `build.yml`), `:preprod` (dernier build
de `dev`) et `:production` (la révision promue). **Jamais `:prod`** :
`ghcr.io/<owner>/lodb/nginx` est partagé avec l'ancienne stack, dont les déploiements
tirent `:prod`, et le retour arrière de la bascule a besoin de cette image intacte.

## Ce que fait le job sur l'hôte, dans l'ordre

1. Synchronise le dépôt sur la branche, contrôle le `.env` (lignes marquées ⚙️ dans
   [`configuration.md`](configuration.md)), le réseau de la base (créé sur `preprod`, exigé en
   prod) et le réseau `edge` (exigé, jamais créé).
2. Cherche les conteneurs **d'autres projets** dont les labels Caddy réclament un domaine
   de `CADDY_DOMAINS` ou `API_CADDY_DOMAINS`. S'il en trouve sans `take_over_domains`, il
   s'arrête là, sans rien avoir modifié.
3. `docker compose pull` (5 tentatives), puis `docker compose run --rm migrate` : la base
   passe à la dernière migration (une base Doctrine est d'abord marquée à `Baseline`).
   Un échec arrête le job ; les conteneurs en place continuent de servir.
4. Avec `take_over_domains` : arrête (`docker stop`) les conteneurs trouvés au point 2.
5. `docker compose up -d --no-build --wait`, puis smoke test dans la stack. Si l'un échoue
   après le point 4, le job arrête le nginx de la nouvelle stack et **redémarre** les
   conteneurs arrêtés : les domaines reviennent à leur ancien propriétaire.
6. Annonce la révision servie (`::notice` « Deployed revision »), qui doit égaler la
   révision promue en prod, puis contrôle le TLS public (avec relance de l'edge).

## Avant le premier déploiement de `preprod`

1. Tout ce que [`configuration.md`](configuration.md) liste pour `preprod` est en place : hôte
   (Docker, edge `infra-vps`, DNS de `CADDY_DOMAINS` **et** de `API_CADDY_DOMAINS`),
   secrets `ENV_FILE` et `DATA_PROTECTION_PFX`.
2. Les packages GHCR `lodb/api` et `lodb/web-ssr` sont nouveaux : leur visibilité se règle
   comme celle des autres. `lodb/nginx` est partagé avec l'ancienne stack, qui n'utilise
   jamais les tags `preprod` et `production`.
3. Base de `preprod` : un dump anonymisé (`tools/db/anonymize.sh`) restauré dans le
   Postgres de la stack avant le premier `migrate` (`docker compose up -d --wait postgres`,
   puis `pg_restore --no-owner --no-privileges`). Sans dump, `migrate` part d'une base vide.
4. Un push de `dev` qui touche la nouvelle stack déclenche le premier déploiement ; sinon,
   **Actions ▸ Deploy ▸ Run workflow** sur `dev`.

## Promouvoir en prod, et bascule

1. Relever la révision annoncée par le dernier déploiement de `preprod` validé.
2. **Actions ▸ Promote** : `revision` = ce SHA complet. Le job `candidate` vérifie
   les trois images (et signale si `:preprod` a bougé depuis) ; les reviewers de
   `production` approuvent ; le retag puis le déploiement suivent.
3. **Bascule** (fenêtre du [runbook](../reecriture/bascule.md), sauvegarde de la base
   faite) : même promotion avec `take_over_domains` coché. Les migrations additives
   passent pendant que l'ancienne stack sert encore ; ses conteneurs porteurs des domaines
   (`nginx`, `go-api` de `lodb-prod`) ne sont arrêtés qu'ensuite. Son Postgres, qui est la
   base partagée, ne s'arrête jamais.
4. L'ancienne prod n'est plus redéployée : son workflow (`ci.yml`) est archivé. Ne pas le
   remettre en service après la bascule : son `up -d` redémarrerait les conteneurs
   arrêtés, qui réclameraient à nouveau les domaines.

**Retour arrière** manuel, tant que le schéma reste compatible (migrations additives) ;
critères et contrôles : [runbook de bascule](../reecriture/bascule.md), § 8.

```bash
# La nouvelle stack rend les domaines et arrête ses tâches de fond, puis l'ancienne reprend
# ses conteneurs arrêtés (start, jamais up) ; son PostgreSQL n'a jamais été arrêté.
cd "$PRODUCTION_PATH" && COMPOSE_FILE=compose.yaml:compose.deploy.yaml \
  docker compose stop nginx web-ssr api
cd "$PROD_PATH" && COMPOSE_FILE=compose.yaml:compose.deploy.yaml docker compose start
```

`$PROD_PATH` est le dossier de l'ancienne prod sur l'hôte, resté sur sa dernière révision
déployée (compose à sa racine) : l'archivage sous `legacy/` ne l'atteint pas, puisque plus
aucun job ne le met à jour.

Revenir à une révision précédente de la nouvelle stack : relancer *Promote* avec son
SHA.
