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
| `ci.yml` (*CI/CD*) | Orchestrateur. Déclenché par `push` (`dev`, `main` : les seules branches qui déploient) et `pull_request`, filtré sur les chemins de la nouvelle stack. Checks parallèles, **sauf sur `main`** : `dotnet` (build + tests, Testcontainers), `front` (lint, typecheck, tests, `build:web`, `build:shell`), `contract` (`api:check`), `i18n` (`i18n:report`, non bloquant), `workflows` (actions épinglées, actionlint, outils de déploiement), `supply-chain` (images de base épinglées par digest, `npm audit` des dépendances de production du front : bloquant dès `high`, exceptions datées dans `tools/ci/npm-audit-allowlist.json`), `e2e` (stack `lodb-dev`, grammaire d'URL `tools/routing/check-urls.sh`, image nginx `tools/nginx`, puis Playwright). Sur un push de **`dev`**, checks bloquants verts : `merge-to-test`, `build`, `deploy-staging`, puis `staging-validated` (pose `:staging-validated` sur les digests que `staging` vient de servir, smoke tests passés). Sur un push de **`main`** : `promote`, `deploy-prod`. |
| `_build.yml` | Réutilisable : résout `test` en **un** commit et en construit les images `ghcr.io/<owner>/lodb/{api,web-ssr,nginx}`, poussées sous `:<sha>` (ce commit de `test`) avec leur SBOM et leur attestation de provenance ; label OCI et `APP_REVISION` = ce SHA. Trivy scanne chaque image poussée et **bloque** sur une vulnérabilité HIGH ou CRITICAL corrigée en amont ; `:staging` n'est posé qu'ensuite, sur les digests scannés ([`tools/ci/README.md`](../../tools/ci/README.md)). |
| `_deploy.yml` | Réutilisable : déploiement SSH d'un hôte, même logique pour `staging` et `prod`. Entrées `environment` (`staging` ou `prod`) et `branch` (`test` ou `main`) ; secrets passés par `ci.yml` (§ Secrets). Son job tourne dans l'environnement GitHub `staging` ou `production`. |
| `_promote.yml` | Réutilisable : pose `:prod` et `:latest` sur les digests que `staging` a validés, **sans rebuild** (`tools/ci/promote-images.sh`). Refuse si `:staging` ne désigne plus ces digests, ou si leur révision n'a pas le code du commit promu. |
| `release-desktop.yml`, `release-android.yml` | Releases signées des apps ([`release-desktop.md`](release-desktop.md), [`release-android.md`](release-android.md)). |

```
push dev  ─▶ checks (dotnet, front, contract, workflows, supply-chain, e2e ; i18n informatif)
          ─▶ merge dev → test (créée depuis main si absente)
          ─▶ _build (GHCR :<sha> ─▶ scan Trivy ─▶ :staging)
          ─▶ _deploy staging (branche test) : pull ─▶ migrate ─▶ up -d ─▶ smoke test
          ─▶ :staging-validated (les digests servis)
fusion manuelle test → main ─▶ push main
          ─▶ _promote (:staging-validated → :prod + :latest, par digest, sans rebuild)
          ─▶ _deploy prod (branche main) : pull ─▶ migrate ─▶ up -d ─▶ smoke test
```

- `test` ne déclenche jamais le workflow : c'est la branche que l'hôte de `staging` suit.
- `main` n'est atteint que par la fusion manuelle de `test` : c'est la validation humaine
  qui met en prod. La prod reçoit les images que `staging` a servies, sans rebuild ni
  nouveau passage des checks.
- `_promote` ne promeut que les digests que `staging` a servis avec succès
  (`:staging-validated`), et seulement si `:staging` les désigne encore et s'ils ont été
  construits depuis le code de `main` (chemins surveillés par `ci.yml`). Un push de `dev`
  entre la validation de `staging` et la fusion dans `main`, ou un déploiement de `staging`
  en échec, **arrête** la promotion au lieu d'envoyer en prod une image que `staging` n'a pas
  servie. Une fois `staging` vert sur le code de `main`, relancer le run refusé (*Re-run
  failed jobs* : `promote`, puis `deploy-prod`). Ne fusionner à nouveau `test` dans `main`
  que si `main` n'a pas le code que `staging` sert : quand `main` contient déjà `test`, la
  fusion n'a rien à fusionner et ne lance aucun run.
- Filtre de chemins : un push qui ne touche pas la nouvelle stack (documentation seule) ne
  lance rien, ni fusion dans `test` ni promotion.
- Sur `dev` et `main`, un push plus récent **attend** la fin du run en cours au lieu de
  l'annuler : ce run se termine par un déploiement, qu'une annulation couperait en plein
  milieu. Ailleurs (PR, autres branches), le run obsolète est annulé.

## Secrets

Les checks, la fusion dans `test`, le build, le retag et le `pull` des images sur l'hôte
n'utilisent que `GITHUB_TOKEN`.
Le déploiement lit des **secrets de dépôt**, comme l'ancienne stack, dans l'environnement
GitHub de sa cible (`staging`, `production`), où l'exploitant pose les protections
([`configuration.md`](configuration.md), § 3.3) ; `ci.yml` les passe à `_deploy.yml` :

| Entrée de `_deploy.yml` | `deploy-staging` | `deploy-prod` |
|---|---|---|
| `SSH_KEY` | `STAGING_SSH_KEY` | `PROD_SSH_KEY` |
| `SSH_HOST` | `STAGING_HOST` | `PROD_HOST` |
| `DEPLOY_PATH` | `STAGING_PATH` | `PROD_PATH` |
| `SSH_USER` (➖, `root`) | `STAGING_SSH_USER` | `PROD_SSH_USER` |
| `SSH_KNOWN_HOSTS` (➖) | `STAGING_SSH_KNOWN_HOSTS` | `PROD_SSH_KNOWN_HOSTS` |
| `ENV_FILE` | `ENV_STAGING` | `ENV_PROD` |
| `DATA_PROTECTION_PFX` | `STAGING_DATA_PROTECTION_PFX` | `PROD_DATA_PROTECTION_PFX` |

Les quatre premières lignes existent déjà (ancienne stack). `ENV_STAGING` et `ENV_PROD`
existent aussi, mais leur contenu est **remplacé** par le `.env` de la nouvelle stack ; les
deux certificats sont nouveaux ([`configuration.md`](configuration.md), § 2 et 3.2). Les
clés de l'hôte (`*_SSH_KNOWN_HOSTS`) sont à créer (§ 6.3 du même guide) : absentes, le job
les lit par `ssh-keyscan`, sans vérification, et le run porte un avertissement.

Le stockage des pages pré-rendues n'ajoute **aucun secret GitHub** : la clé de son application
d'écriture entre dans `ENV_STAGING` et `ENV_PROD` (lignes 🔒 `LoDb__Prerender__AccessKey` et
`LoDb__Prerender__SecretKey`), que le job range en fichiers secrets comme les autres
([`configuration.md`](configuration.md), § 4 et 4.8). La clé temporaire de l'opérateur, qui
configure le bucket, ne quitte jamais son poste ([`tools/prerender/README.md`](../../tools/prerender/README.md)).
La facture Scaleway de l'administration n'en ajoute pas non plus : sa clé de lecture
(`LoDb__ScalewayBilling__SecretKey`, une par environnement) entre aussi dans `ENV_STAGING` et
`ENV_PROD` ([`configuration.md`](configuration.md), § 4.9).

**Tags d'images** : `:<sha>` (immuable, poussé par `_build.yml` : le commit de `test`
construit, que la notice *Deployed revision* du déploiement affiche), `:staging` (dernier
build de `dev` dont le scan est passé), `:staging-validated` (ce que `staging` a servi, smoke
tests passés), `:prod` et `:latest` (images promues, mêmes digests). Chaque image
porte son SBOM et son attestation de provenance (`docker buildx imagetools inspect
<image> --format '{{json .SBOM}}'`, `.Provenance`). `ghcr.io/<owner>/lodb/nginx` porte le nom
qu'avait l'image nginx de l'ancienne stack : ses `:staging` et `:prod` désignent désormais
la nouvelle, et les images de l'ancienne restent accessibles par leur tag `:<sha>`.

## Ce que fait le job sur l'hôte, dans l'ordre

La nouvelle stack prend la place de l'ancienne dans le même projet Compose
(`lodb-staging`, `lodb-prod`) et le même dossier (`STAGING_PATH`, `PROD_PATH`).

1. Garde le `.env` en place sous `.deploy/backups/`, pose `ENV_*` dans
   `.deploy/incoming.env` (root seul) et `.deploy/data-protection.pfx`, puis synchronise le
   dépôt sur la branche (`git reset --hard`). `tools/deploy/split-env.sh`, à cette révision,
   sépare alors `ENV_*` dans un dossier temporaire : chaque secret dans un fichier, le reste
   dans un `.env` ([`configuration.md`](configuration.md), § 4). Un secret mal écrit arrête
   le job avant toute écriture.
2. Contrôle ce `.env` (lignes marquées ⚙️ dans [`configuration.md`](configuration.md)) :
   `COMPOSE_PROJECT_NAME` = `lodb-<env>`, `IMAGE_TAG` = `<env>`, `LODB_NOINDEX` (`1` sur
   `staging`, `0` en `prod`), mot de passe de la base présent ; en `prod`, relais SMTP et
   clés Stripe présents (leurs fichiers non vides) ; le mot de passe du rôle des sauvegardes
   hors hôte, s'il est posé, au format d'`infra-vps`. Exige les réseaux `edge` et
   `observability`, plus `sauvegardes` en `prod` (jamais créés, [`sauvegardes.md`](sauvegardes.md)),
   et s'arrête si un autre réseau Docker recouvre le
   sous-réseau de la stack, fixé par cible (`LODB_STACK_SUBNET`, `10.231.1.0/24` en staging,
   `10.231.2.0/24` en prod). Contrôles passés seulement, sépare `ENV_*` en place
   (`.deploy/secrets/`, `.env` en mode 600), supprime `.deploy/incoming.env` et le dossier
   temporaire, puis écrit dans le `.env` le sous-réseau d'`edge` (`LODB_EDGE_CIDR`, seul pair
   dont nginx croit `X-Forwarded-For`), celui de la stack et la valeur de l'en-tête que
   l'edge envoie à nginx (`LODB_EDGE_SECRET`, générée au premier déploiement dans
   `.deploy/secrets/`, jamais dans `ENV_*`) ; s'arrête si la configuration résolue fait
   croire à l'API ou au SSR un autre réseau que celui-là.
3. `docker compose pull` (5 tentatives), sauvegarde de la base en service (`.deploy/backups/`), l'hôte connecté à GHCR avec le `GITHUB_TOKEN` du
   run le temps du seul `pull` (transmis par l'entrée de `ssh`, jamais en ligne de
   commande). Si le réseau de la stack existe sur un autre sous-réseau, `docker compose
   down` (sans `-v`) le libère une fois (environ une minute de coupure, page de maintenance
   affichée, voir l'étape 4). Puis
   `docker compose run --rm migrate` : la base du
   volume `pgdata` passe à la dernière migration (une base Doctrine est d'abord marquée à
   `Baseline`). Un échec arrête le job ; les conteneurs en place continuent de servir, sauf
   juste après ce changement de réseau : la stack reste alors arrêtée jusqu'au déploiement
   suivant.
4. Page de maintenance (`tools/deploy/maintenance.sh start`, `.deploy/maintenance/`) : de ce
   point aux sondes de la nouvelle stack, chaque page du site répond 503 par la page de
   maintenance, dans la langue et l'identité du visiteur, avec le temps restant estimé sur la
   durée du déploiement précédent, mesurée et gardée sur l'hôte. Puis
   `docker compose up -d --no-build --remove-orphans --wait` : les conteneurs de la nouvelle
   stack remplacent ceux de l'ancienne, dont `php`, `go-fetcher`, `go-api` et `mailer` sont
   retirés comme orphelins. Les sondes passées, la page de maintenance est retirée et la durée
   du passage gardée pour le déploiement suivant ; un échec avant ce point la laisse en place
   (`::error` « Maintenance page left on », à retirer à la main une fois la stack rétablie,
   [`configuration.md`](configuration.md), § 5). `pgdata` est gardé ; les blobs vont dans le
   volume `ddragon` ; `storage` et `app_state` restent intacts. Si le `.env` définit
   `LODB_ADMIN_LOGIN`, `api admin root` tient l'administrateur racine (mot de passe passé par
   l'entrée de la commande, sortie hors du journal public).
5. Smoke test dans la stack (`/healthz` par nginx, `/readyz` de l'API, `/en/`, sous-domaine
   `api.`, `403` sans l'en-tête de l'edge, `X-Robots-Tag`), annonce la révision servie
   (`::notice` « Deployed revision »), puis contrôle le TLS public (avec relance de l'edge)
   et, par l'edge, que nginx accepte ses requêtes.

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
