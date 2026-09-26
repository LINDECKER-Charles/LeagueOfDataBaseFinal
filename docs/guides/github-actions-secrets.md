# 🔐 GitHub Actions — Secrets

Secrets à configurer pour le pipeline `CI/CD` (`.github/workflows/ci.yml`).

**Où** : `Settings` → `Secrets and variables` → `Actions` → onglet `Repository secrets`.

## 🔁 Flux (promotion à deux étages, *build once*)

```
push dev  ─▶ _tests ─▶ merge-to-test (dev→test)
          ─▶ _build (GHCR: :<sha> + :staging) ─▶ _deploy staging  (test.league-of-data-base.com)

merge test→main (manuel)  ─▶ push main
          ─▶ _promote : retag :staging→:prod (sans rebuild) ─▶ _deploy prod (league-of-data-base.fr/.com)
```

### 🧩 Structure des workflows (reusable, responsabilité unique)

| Fichier | Rôle |
|---|---|
| `ci.yml` | Orchestrateur : déclencheurs `dev`/`main`, gardes `if`, câblage. Aucun secret en dur. |
| `_tests.yml` | PHP / Go / JS. |
| `_build.yml` | Build + push des 3 images (`:<sha>` + tag mouvant). |
| `_deploy.yml` | Déploiement SSH d'**un** hôte, **paramétré** → appelé pour staging **et** prod (DRY). |
| `_promote.yml` | Retag `:staging → :prod` (aucun rebuild). |

- `test` est mis à jour automatiquement depuis `dev` et **ne déclenche jamais** le workflow (pas de boucle).
- `main` n'est atteint que par un **merge manuel `test → main`** : c'est le *gate* humain qui met en production.
- La prod **ne rebuild pas** : elle retague et déploie **exactement l'image validée en staging**
  (`:staging` courant → `:prod`). Corollaire : toujours passer par `test → main` — un push direct
  sur `main` déploierait l'image staging courante, pas le code poussé.

---

## 🧪 Tests appli (workflow `_tests.yml` — jobs `php` / `go` / `js`)

| Secret | Requis | Description |
|---|:---:|---|
| `ENV_TEST` | ✅ | Dotenv de test appli **complet** (source : `.env.test`). Écrit dans `app/.env.test.local` avant PHPUnit / lints. **Sans rapport avec le déploiement staging.** |

---

## 🟡 Déploiement staging (job `deploy-staging`)

Son `.env` doit fixer `COMPOSE_PROJECT_NAME=lodb-staging`, `IMAGE_TAG=staging` et
`CADDY_DOMAINS=test.league-of-data-base.com`. Le VPS peut être mutualisé avec la
prod (et d'autres projets) : l'isolation vient du `COMPOSE_PROJECT_NAME` distinct
et le TLS d'un **edge proxy partagé**, fourni par le dépôt d'infrastructure `infra-vps`
(section TLS ci-dessous).

| Secret | Requis | Description |
|---|:---:|---|
| `STAGING_SSH_KEY` | ✅ | Clé privée SSH (PEM complet) chargée dans `ssh-agent`. Clé publique dans les `authorized_keys` du serveur staging. |
| `STAGING_HOST` | ✅ | Hôte staging (IP ou FQDN). `ssh-keyscan` + connexions SSH. |
| `STAGING_PATH` | ✅ | Chemin absolu du projet sur le serveur (dossier des `compose.*.yaml`). Cible du `git pull origin test` et du `docker compose`. |
| `STAGING_SSH_USER` | ➖ | Utilisateur SSH. **Optionnel**, défaut `root`. |
| `ENV_STAGING` | ✅ | Dotenv staging **complet**. Poussé dans `${STAGING_PATH}/.env`. Doit inclure `COMPOSE_PROJECT_NAME=lodb-staging`, `REGISTRY=ghcr.io/<owner>/lodb`, `IMAGE_TAG=staging`, les secrets applicatifs (`APP_SECRET`, `ADMIN_*`, `POSTGRES_PASSWORD`, `STRIPE_*` — cf. section base de données & Stripe) **et** `CADDY_DOMAINS=test.league-of-data-base.com`. Aucun paramètre de l'edge (contact Let's Encrypt) ici : il vit dans `infra-vps`. |

---

## 🚀 Déploiement production (jobs `promote` + `deploy-prod`)

Son `.env` doit fixer `COMPOSE_PROJECT_NAME=lodb-prod`, `IMAGE_TAG=prod` et les domaines apex.
Peut cohabiter avec staging sur le même VPS (projets Compose distincts + edge partagé).

| Secret | Requis | Description |
|---|:---:|---|
| `PROD_SSH_KEY` | ✅ | Clé privée SSH (PEM complet) chargée dans `ssh-agent`. Clé publique dans les `authorized_keys` du serveur prod. |
| `PROD_HOST` | ✅ | Hôte prod (IP ou FQDN). `ssh-keyscan` + connexions SSH. |
| `PROD_PATH` | ✅ | Chemin absolu du projet sur le serveur. Cible du `git pull origin main` et du `docker compose`. |
| `PROD_SSH_USER` | ➖ | Utilisateur SSH. **Optionnel**, défaut `root`. |
| `ENV_PROD` | ✅ | Dotenv prod **complet**. Poussé dans `${PROD_PATH}/.env`. Doit inclure `COMPOSE_PROJECT_NAME=lodb-prod`, `REGISTRY=ghcr.io/<owner>/lodb`, `IMAGE_TAG=prod`, les secrets applicatifs (dont `POSTGRES_PASSWORD` et `STRIPE_*` — cf. section suivante) **et** `CADDY_DOMAINS=league-of-data-base.fr, league-of-data-base.com`. Aucun paramètre de l'edge (contact Let's Encrypt) ici : il vit dans `infra-vps`. |

---

## 🗄️ Base de données & Stripe — lignes à porter dans `ENV_STAGING` / `ENV_PROD` / `ENV_TEST`

Ces variables sont des **lignes des dotenv** ci-dessus (pas des secrets GitHub distincts).

| Variable | Où | Description |
|---|---|---|
| `POSTGRES_PASSWORD` | `ENV_STAGING`, `ENV_PROD` | Mot de passe du service Postgres du stack. **Fort et unique par environnement** — le défaut compose (`lodb`) n'est acceptable qu'en dev local. |
| `DATABASE_URL` | optionnel | Assemblée par le compose depuis `POSTGRES_*` ; ne la définir explicitement que pour pointer une base **externe** au stack (format `postgresql://user:pass@host:5432/db?serverVersion=17&charset=utf8`). |
| `STRIPE_SECRET_KEY` | `ENV_PROD` (`sk_live_…`), `ENV_STAGING`/`ENV_TEST` (`sk_test_…`) | Clé API secrète Stripe de la page de don ; vide ⇒ passerelle désactivée proprement. |
| `STRIPE_WEBHOOK_SECRET` | `ENV_STAGING`, `ENV_PROD` | Secret de signature `whsec_…` de l'endpoint `POST /webhooks/stripe` (un endpoint Stripe distinct par environnement). |

> ℹ️ Les tests CI actuels (`ENV_TEST` → PHPUnit `tests/Unit`) ne touchent pas la base.
> Si des tests fonctionnels DB apparaissent un jour, `ENV_TEST` (côté CI) devra porter
> un `DATABASE_URL` pointant un service Postgres du job.

---

## 🔒 TLS / reverse-proxy — edge partagé (caddy-docker-proxy)

Les stacks app **ne publient plus** `80/443` et n'embarquent plus Caddy. Le point
d'entrée TLS est un **edge proxy unique et global** au VPS, partagé par staging, prod
et tout futur projet. Il est **déployé par le dépôt d'infrastructure `infra-vps`
(privé)**, seul propriétaire de l'edge et des réseaux Docker externes `edge` /
`observability` — jamais par ce dépôt. Il détecte les domaines via des **labels**
sur le conteneur nginx et émet/renouvelle seul les certificats Let's Encrypt.
Chaque stack app se contente de déclarer `CADDY_DOMAINS` (→ label) et de rejoindre
le réseau externe `edge` (via `compose.deploy.yaml`). Onboarding d'un nouveau projet :
**`docs/guides/migration-edge-proxy.md`** (« la méthode »).

**Ordre de déploiement** : `infra-vps` doit avoir convergé l'hôte **avant** tout projet
applicatif. Le job `_deploy.yml` ne converge plus rien côté edge : il **vérifie** seulement
que le réseau `edge` existe et **échoue explicitement** sinon (« deploy infra-vps before
this project »). Aucun secret lié à l'edge n'est requis dans ce dépôt : le contact
Let's Encrypt est configuré dans `infra-vps`.

| Variable | Où | Staging | Prod |
|---|---|---|---|
| `CADDY_DOMAINS` | `ENV_STAGING`/`ENV_PROD` | `test.league-of-data-base.com` | `league-of-data-base.fr, league-of-data-base.com` |

> ⚠️ **Ordre au premier déploiement** : les enregistrements DNS (A/AAAA) de chaque
> domaine doivent pointer vers le VPS **avant** que le déploiement tourne, et les
> ports **80 + 443** doivent être joignables — l'émission ACME échoue sinon. Caddy
> réessaie tout seul une fois le DNS propagé.

### 🖥️ Prérequis serveur (one-shot)

**Sur le VPS, une fois** :

1. Docker Engine + plugin `docker compose` installés.
2. **Déployer `infra-vps` d'abord** : edge (caddy-docker-proxy) + réseaux `edge` et
   `observability`. Sans ça, le job de ce dépôt s'arrête sur l'assertion réseau.
3. Ports **80/443** ouverts et DNS des domaines pointé (cf. ci-dessus).
4. `docker login ghcr.io` persistant si les packages GHCR sont **privés** (PAT `read:packages`),
   sinon les rendre publics — sans ça `docker compose pull` échoue.
5. Le `*_PATH` peut être vide : le job initialise le dépôt (`git init` + `reset`), pousse le `.env`,
   vérifie le réseau `edge`, puis déploie. staging suit `test`, prod suit `main`.

---

## ⚙️ Secrets automatiques (aucune action requise)

| Secret | Description |
|---|---|
| `GITHUB_TOKEN` | Fourni automatiquement par GitHub Actions. Utilisé pour le merge `dev → test`, le push et le retag des images sur GHCR. **Ne pas créer manuellement.** |

---

## 📝 Notes

- **Mapping fichier → secret** : le dotenv local de chaque environnement devient le secret correspondant.
  - `.env.test` → secret **`ENV_TEST`** → env de test appli (PHPUnit/CI ; écrit dans `app/.env.test.local`)
  - `.env.staging` → secret **`ENV_STAGING`** → déploiement staging (`test.league-of-data-base.com`)
  - `.env.prod` → secret **`ENV_PROD`** → déploiement prod
  - ⚠️ `test` (env applicatif) ≠ `staging` (déploiement pré-prod) : deux choses distinctes, deux fichiers, deux secrets.
- **`CADDY_DOMAINS`** est une **ligne** de ces dotenv (donc dans `ENV_STAGING` / `ENV_PROD`), jamais dans les workflows. Le contact Let's Encrypt de l'edge ne s'y trouve pas : il est géré par `infra-vps`.
- **`ENV_*`** contiennent le fichier dotenv intégral (une variable par ligne), pas une valeur unique — coller le contenu complet du `.env` correspondant.
- **Staging et prod ne diffèrent que par leur `.env`** (`COMPOSE_PROJECT_NAME`, `IMAGE_TAG`, `CADDY_DOMAINS`, secrets) : mêmes fichiers compose. Le `COMPOSE_PROJECT_NAME` distinct est ce qui les isole sur un VPS mutualisé.
- **Plus aucune variable `MINIO_*`** : le stockage Data Dragon est un volume Docker (`storage`), sans identifiants ni bucket. Les lignes `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` encore présentes dans `ENV_STAGING` / `ENV_PROD` ne sont plus lues et peuvent être retirées. L'ancien volume `<projet>_minio_data` reste sur l'hôte jusqu'à suppression manuelle (`docker volume rm lodb-staging_minio_data`, idem `lodb-prod_minio_data`).
- **`*_SSH_KEY`** : copier l'intégralité du fichier clé, en-têtes `-----BEGIN … PRIVATE KEY-----` / `-----END … PRIVATE KEY-----` inclus.
- Les secrets ne sont **jamais** affichés dans les logs (masqués par GitHub) ; leur mise à jour ne s'applique qu'aux exécutions suivantes.

---

## 🆕 Nouvelle stack (`lodb-next`, réécriture .NET + Angular)

Workflows propres à la réécriture, indépendants de `ci.yml` et des `_*.yml` ci-dessus :

| Fichier | Rôle |
|---|---|
| `next-ci.yml` | Déclenché par `push` (branche d'intégration `docs/reecriture-dotnet-angular`, `dev`, `main`) et `pull_request`, filtré sur les chemins de la nouvelle stack. Jobs parallèles : `dotnet` (build + tests, Testcontainers), `front` (lint, typecheck, tests, `build:web`, `build:shell`), `contract` (`api:check`), `i18n` (`i18n:report`, non bloquant), `e2e` (stack `lodb-next` + Playwright). |
| `next-build.yml` | Réutilisable, appelé par `next-ci.yml` sur `push` de la branche d'intégration une fois les jobs bloquants verts : images `ghcr.io/<owner>/lodb/{api,web-ssr,nginx}` taguées `:<sha>` + `:next`, label OCI et `APP_REVISION` = SHA. |
| `next-deploy.yml` | Manuel (`workflow_dispatch`, entrée `branch`) : déploie l'environnement `next` par SSH. `pull`, puis service éphémère `migrate` (dès L1.4) **avant** `up -d --wait`, puis smoke test (nginx, `/readyz`, `/en/`, TLS public avec relance de l'edge). |

```
push docs/reecriture-dotnet-angular ─▶ next-ci (dotnet, front, contract, e2e ; i18n informatif)
                                    ─▶ next-build (GHCR :<sha> + :next)
Actions ▸ next deploy ▸ Run workflow ─▶ hôte next : pull ─▶ migrate ─▶ up -d ─▶ smoke test
```

Les jobs de CI et de build n'utilisent aucun secret (seulement `GITHUB_TOKEN`). Le job de
déploiement tourne dans l'environnement GitHub `next`, créé à la première exécution : ses
secrets peuvent y être déclarés (ou en secrets de dépôt) et des règles de protection
ajoutées.

### Secrets du déploiement `next`

| Secret | Requis | Description |
|---|:---:|---|
| `NEXT_SSH_KEY` | ✅ | Clé privée SSH (PEM complet) chargée dans `ssh-agent`. Clé publique dans les `authorized_keys` de l'hôte. |
| `NEXT_HOST` | ✅ | Hôte `next` (IP ou FQDN), le même VPS que staging et prod le cas échéant. |
| `NEXT_PATH` | ✅ | Chemin absolu du projet sur l'hôte, **distinct** de `STAGING_PATH` et `PROD_PATH` (chaque dossier a son `.env`). Le job y initialise le dépôt et suit la branche passée en entrée. |
| `NEXT_SSH_USER` | ➖ | Utilisateur SSH. **Optionnel**, défaut `root`. |
| `ENV_NEXT` | ✅ | Dotenv `next` **complet**, écrit dans `${NEXT_PATH}/.env`. Modèle : `.env.next.example`. |

### Lignes de `ENV_NEXT`

| Variable | Description |
|---|---|
| `COMPOSE_PROJECT_NAME` | `lodb-next`. Le job refuse de déployer sans elle (isolation des stacks du VPS). |
| `REGISTRY`, `IMAGE_TAG` | `ghcr.io/<owner>/lodb` et `next` (images poussées par `next-build.yml`). |
| `CADDY_DOMAINS` | Domaine du site `next` (label Caddy `caddy_0`). |
| `API_CADDY_DOMAINS` | Sous-domaine de l'API publique, qui **doit commencer par `api.`** (nginx le reconnaît à ce préfixe). Label `caddy_1`. |
| `LODB_CANONICAL_HOST` | Hôte cible des 301 `www.` et `.fr`. |
| `LODB_ALLOWED_HOSTS` | Hôtes acceptés par le serveur SSR, séparés par des virgules (ceux de `CADDY_DOMAINS`). |
| `LODB_EDGE_CIDR` | Sous-réseau du réseau Docker `edge`, seul pair cru sur `X-Forwarded-For` : `docker network inspect edge --format '{{(index .IPAM.Config 0).Subnet}}'`. |
| `LODB_NOINDEX` | `1` sur `next` : `X-Robots-Tag: noindex, nofollow` sur toutes les réponses. |
| `LODB_DB_PASSWORD` | Mot de passe du Postgres propre à `next`, fort et unique. `LODB_DB_NAME` et `LODB_DB_USER` valent `lodb` par défaut. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Optionnel : collecteur de traces, quand `infra-vps` en expose un. |

### Avant le premier déploiement de `next`

1. Les prérequis serveur ci-dessus (edge `infra-vps`, `docker login ghcr.io` si les
   packages sont privés) valent aussi pour `next` ; DNS de `CADDY_DOMAINS` **et** de
   `API_CADDY_DOMAINS` pointés vers l'hôte.
2. La branche d'intégration doit être poussée (l'hôte la récupère) et avoir produit des
   images `:next` (un `push` avec `next-ci.yml` vert).
3. Les packages GHCR `lodb/api` et `lodb/web-ssr` sont nouveaux : leur visibilité se règle
   comme celle des autres. `lodb/nginx` est partagé avec l'ancienne stack, qui n'utilise
   jamais le tag `next`.
