# 🔐 GitHub Actions — pipeline CI/CD

Ce guide décrit le pipeline : qui construit, qui déploie, dans quel ordre. **La liste de
tout ce qu'il faut configurer** (secrets, variables, lignes des `.env`, fichiers de l'hôte,
services externes) est dans [`configuration.md`](configuration.md).

Le pipeline garde les conventions de l'ancienne stack : mêmes noms de workflows, même
chemin `dev` → `test` → `main`, mêmes environnements (`staging`, `prod`) et mêmes secrets.
Ses workflows d'origine sont archivés sous `legacy/.github/workflows/` et ne tournent
plus ; leur guide est
[`legacy/docs/guides/github-actions-secrets.md`](../../legacy/docs/guides/github-actions-secrets.md).

## Workflows

| Fichier | Rôle |
|---|---|
| `ci.yml` (*CI/CD*) | Orchestrateur. Déclenché par `push` (`dev`, `main`, `docs/reecriture-dotnet-angular`) et `pull_request`, filtré sur les chemins de la nouvelle stack. Checks parallèles, **sauf sur `main`** : `dotnet` (build + tests, Testcontainers), `front` (lint, typecheck, tests, `build:web`, `build:shell`), `contract` (`api:check`), `i18n` (`i18n:report`, non bloquant), `e2e` (stack `lodb-dev` + Playwright). Sur un push de **`dev`**, checks bloquants verts : `merge-to-test`, `build`, `deploy-staging`. Sur un push de **`main`** : `promote`, `deploy-prod`. |
| `_build.yml` | Réutilisable : construit depuis `test` les images `ghcr.io/<owner>/lodb/{api,web-ssr,nginx}`, taguées `:<sha>` (le commit poussé sur `dev`) + `:staging`, label OCI et `APP_REVISION` = SHA. |
| `_deploy.yml` | Réutilisable : déploiement SSH d'un hôte, même logique pour `staging` et `prod`. Entrées `environment` (`staging` ou `prod`) et `branch` (`test` ou `main`) ; secrets passés par `ci.yml` (§ Secrets). |
| `_promote.yml` | Réutilisable : retague `:staging` en `:prod` et `:latest`, **sans rebuild**. |
| `release-desktop.yml`, `release-android.yml` | Releases signées des apps ([`release-desktop.md`](release-desktop.md), [`release-android.md`](release-android.md)). |

```
push dev  ─▶ checks (dotnet, front, contract, e2e ; i18n informatif)
          ─▶ merge dev → test (créée depuis main si absente)
          ─▶ _build (GHCR :<sha> + :staging)
          ─▶ _deploy staging (branche test) : pull ─▶ migrate ─▶ up -d ─▶ smoke test
fusion manuelle test → main ─▶ push main
          ─▶ _promote (:staging → :prod + :latest, sans rebuild)
          ─▶ _deploy prod (branche main) : pull ─▶ migrate ─▶ up -d ─▶ smoke test
```

- `test` ne déclenche jamais le workflow : c'est la branche que l'hôte de `staging` suit.
- `main` n'est atteint que par la fusion manuelle de `test` : c'est la validation humaine
  qui met en prod. La prod reçoit les images que `staging` a servies, sans rebuild ni
  nouveau passage des checks.
- `_promote` promeut ce que `:staging` désigne **au moment du push de `main`**, c'est-à-dire
  le dernier build de `dev`. Ne pas pousser `dev` entre la validation de `staging` et la
  fusion dans `main`.
- Filtre de chemins : un push qui ne touche pas la nouvelle stack (documentation seule) ne
  lance rien, ni fusion dans `test` ni promotion.
- Sur `dev` et `main`, un push plus récent **attend** la fin du run en cours au lieu de
  l'annuler : ce run se termine par un déploiement, qu'une annulation couperait en plein
  milieu. Ailleurs (PR, autres branches), le run obsolète est annulé.

## Secrets

Les checks, la fusion dans `test`, le build, le retag et le `pull` des images sur l'hôte
n'utilisent que `GITHUB_TOKEN`.
Le déploiement lit des **secrets de dépôt**, sans environnement GitHub, comme l'ancienne
stack ; `ci.yml` les passe à `_deploy.yml` :

| Entrée de `_deploy.yml` | `deploy-staging` | `deploy-prod` |
|---|---|---|
| `SSH_KEY` | `STAGING_SSH_KEY` | `PROD_SSH_KEY` |
| `SSH_HOST` | `STAGING_HOST` | `PROD_HOST` |
| `DEPLOY_PATH` | `STAGING_PATH` | `PROD_PATH` |
| `SSH_USER` (➖, `root`) | `STAGING_SSH_USER` | `PROD_SSH_USER` |
| `ENV_FILE` | `ENV_STAGING` | `ENV_PROD` |
| `DATA_PROTECTION_PFX` | `STAGING_DATA_PROTECTION_PFX` | `PROD_DATA_PROTECTION_PFX` |

Les quatre premières lignes existent déjà (ancienne stack). `ENV_STAGING` et `ENV_PROD`
existent aussi, mais leur contenu est **remplacé** par le `.env` de la nouvelle stack ; les
deux certificats sont nouveaux ([`configuration.md`](configuration.md), § 2 et 3.2).

**Tags d'images** : `:<sha>` (immuable, poussé par `_build.yml`), `:staging` (dernier build
de `dev`), `:prod` et `:latest` (images promues). `ghcr.io/<owner>/lodb/nginx` porte le nom
qu'avait l'image nginx de l'ancienne stack : ses `:staging` et `:prod` désignent désormais
la nouvelle, et les images de l'ancienne restent accessibles par leur tag `:<sha>`.

## Ce que fait le job sur l'hôte, dans l'ordre

La nouvelle stack prend la place de l'ancienne dans le même projet Compose
(`lodb-staging`, `lodb-prod`) et le même dossier (`STAGING_PATH`, `PROD_PATH`).

1. Écrit le `.env` (mode 600) et `.deploy/data-protection.pfx`, puis synchronise le dépôt
   sur la branche (`git reset --hard`).
2. Contrôle le `.env` (lignes marquées ⚙️ dans [`configuration.md`](configuration.md)) :
   `COMPOSE_PROJECT_NAME` = `lodb-<env>`, `IMAGE_TAG` = `<env>`, `LODB_NOINDEX` (`1` sur
   `staging`, `0` en `prod`) ; en `prod`, relais SMTP et clés Stripe présents. Exige le
   réseau `edge` (jamais créé) et écrit son sous-réseau dans le `.env` (`LODB_EDGE_CIDR`,
   seul pair dont nginx croit `X-Forwarded-For`).
3. `docker compose pull` (5 tentatives), l'hôte connecté à GHCR avec le `GITHUB_TOKEN` du
   run le temps du seul `pull` (transmis par l'entrée de `ssh`, jamais en ligne de
   commande), puis `docker compose run --rm migrate` : la base du
   volume `pgdata` passe à la dernière migration (une base Doctrine est d'abord marquée à
   `Baseline`). Un échec arrête le job ; les conteneurs en place continuent de servir.
4. `docker compose up -d --no-build --remove-orphans --wait` : les conteneurs de la nouvelle
   stack remplacent ceux de l'ancienne, dont `php`, `go-fetcher`, `go-api` et `mailer` sont
   retirés comme orphelins. `pgdata` est gardé ; les blobs vont dans le volume `ddragon` ;
   `storage` et `app_state` restent intacts. Si le `.env` définit `LODB_ADMIN_LOGIN`,
   `api admin root` tient l'administrateur racine (mot de passe passé par l'entrée de la
   commande, sortie hors du journal public).
5. Smoke test dans la stack (`/healthz` par nginx, `/readyz` de l'API, `/en/`, sous-domaine
   `api.`, `X-Robots-Tag`), annonce la révision servie (`::notice` « Deployed revision »),
   puis contrôle le TLS public (avec relance de l'edge).

## Basculer `staging`

1. `.env.staging` rempli, valeurs 🔁 relevées dans `$STAGING_PATH/.env` **avant** ce
   déploiement ([`configuration.md`](configuration.md), § 2).
2. `ENV_STAGING` remplacé, `STAGING_DATA_PROTECTION_PFX` créé.
3. Fusion de la bascule dans `dev` : son push déploie `staging`. La base de l'ancien staging
   est gardée et migrée ; le volume `ddragon` part vide et l'ingestion le remplit.

## Mettre en prod, et bascule

1. `staging` validé.
2. Juste avant la fusion : `.env.prod` rempli depuis `$PROD_PATH/.env`, `ENV_PROD` remplacé,
   `PROD_DATA_PROTECTION_PFX` créé. Pas avant : tant que `main` porte l'ancienne stack, un
   push de `main` la redéploierait avec ce `.env`.
3. Fenêtre de bascule du [runbook](../reecriture/bascule.md) (sauvegarde de la base faite).
4. Fusion manuelle de `test` dans `main` : promotion, puis déploiement de `prod`.

**Retour arrière** : [runbook de bascule](../reecriture/bascule.md). Il reste possible tant
que le schéma est compatible (migrations additives) : `pgdata` est partagé, `storage` et
`app_state` sont intacts et les images de l'ancienne stack restent sous leur tag `:<sha>`.

Revenir à une révision précédente de la nouvelle stack : `git revert` sur `dev`, puis le
chemin normal (`staging`, puis fusion dans `main`).
