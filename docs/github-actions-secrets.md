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
et le TLS d'un **edge proxy partagé** (cf. `infra/edge`, section TLS ci-dessous).

| Secret | Requis | Description |
|---|:---:|---|
| `STAGING_SSH_KEY` | ✅ | Clé privée SSH (PEM complet) chargée dans `ssh-agent`. Clé publique dans les `authorized_keys` du serveur staging. |
| `STAGING_HOST` | ✅ | Hôte staging (IP ou FQDN). `ssh-keyscan` + connexions SSH. |
| `STAGING_PATH` | ✅ | Chemin absolu du projet sur le serveur (dossier des `compose.*.yaml`). Cible du `git pull origin test` et du `docker compose`. |
| `STAGING_SSH_USER` | ➖ | Utilisateur SSH. **Optionnel**, défaut `root`. |
| `ENV_STAGING` | ✅ | Dotenv staging **complet**. Poussé dans `${STAGING_PATH}/.env`. Doit inclure `COMPOSE_PROJECT_NAME=lodb-staging`, `REGISTRY=ghcr.io/<owner>/lodb`, `IMAGE_TAG=staging`, les secrets applicatifs (`APP_SECRET`, `MINIO_*`, `ADMIN_*`) **et** `CADDY_DOMAINS=test.league-of-data-base.com`. `ACME_EMAIL` n'est **plus** ici (il vit dans le stack edge). |

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
| `ENV_PROD` | ✅ | Dotenv prod **complet**. Poussé dans `${PROD_PATH}/.env`. Doit inclure `COMPOSE_PROJECT_NAME=lodb-prod`, `REGISTRY=ghcr.io/<owner>/lodb`, `IMAGE_TAG=prod`, les secrets applicatifs **et** `CADDY_DOMAINS=league-of-data-base.fr, league-of-data-base.com`. `ACME_EMAIL` n'est **plus** ici (il vit dans le stack edge). |

---

## 🔒 TLS / reverse-proxy — edge partagé (caddy-docker-proxy)

Les stacks app **ne publient plus** `80/443` et n'embarquent plus Caddy. Le point
d'entrée TLS est un **edge proxy unique et global** au VPS (`infra/edge`), partagé
par staging, prod et tout futur projet. Il détecte les domaines via des **labels**
sur le conteneur nginx et émet/renouvelle seul les certificats Let's Encrypt.
Chaque stack app se contente de déclarer `CADDY_DOMAINS` (→ label) et de rejoindre
le réseau externe `edge` (via `compose.deploy.yaml`). Détails et onboarding d'un
nouveau projet : **`infra/edge/README.md`**.

**L'edge est auto-bootstrappé par la pipeline** (`_deploy.yml`) : à chaque déploiement,
le job (re)converge — de façon idempotente — le réseau `edge`, les fichiers de l'edge
(copiés du repo vers `/opt/edge`) et le proxy lui-même, **avant** de monter l'app. Un
VPS neuf/reconstruit ne demande donc **aucune étape manuelle** pour l'edge ; il faut
seulement fournir le secret `ACME_EMAIL` (contact Let's Encrypt) et pointer le DNS.

| Secret | Requis | Description |
|---|:---:|---|
| `ACME_EMAIL` | ✅ | Contact Let's Encrypt du proxy edge. Partagé staging **et** prod (même VPS). Écrit par le job dans `/opt/edge/.env`. |

| Variable | Où | Staging | Prod |
|---|---|---|---|
| `CADDY_DOMAINS` | `ENV_STAGING`/`ENV_PROD` | `test.league-of-data-base.com` | `league-of-data-base.fr, league-of-data-base.com` |

> ⚠️ **Ordre au premier déploiement** : les enregistrements DNS (A/AAAA) de chaque
> domaine doivent pointer vers le VPS **avant** que le déploiement tourne, et les
> ports **80 + 443** doivent être joignables — l'émission ACME échoue sinon. Caddy
> réessaie tout seul une fois le DNS propagé.

### 🖥️ Prérequis serveur (one-shot)

**Sur le VPS, une fois** (l'edge, lui, est monté automatiquement — cf. `infra/edge/README.md`) :

1. Docker Engine + plugin `docker compose` installés.
2. Ports **80/443** ouverts et DNS des domaines pointé (cf. ci-dessus).
3. `docker login ghcr.io` persistant si les packages GHCR sont **privés** (PAT `read:packages`),
   sinon les rendre publics — sans ça `docker compose pull` échoue.
4. Le `*_PATH` peut être vide : le job initialise le dépôt (`git init` + `reset`), pousse le `.env`,
   bootstrappe l'edge, puis déploie. staging suit `test`, prod suit `main`.

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
- **`CADDY_DOMAINS`** est une **ligne** de ces dotenv (donc dans `ENV_STAGING` / `ENV_PROD`), jamais dans les workflows. **`ACME_EMAIL`** ne s'y trouve plus : il est dans le `.env` du stack edge partagé.
- **`ENV_*`** contiennent le fichier dotenv intégral (une variable par ligne), pas une valeur unique — coller le contenu complet du `.env` correspondant.
- **Staging et prod ne diffèrent que par leur `.env`** (`COMPOSE_PROJECT_NAME`, `IMAGE_TAG`, `CADDY_DOMAINS`, secrets) : mêmes fichiers compose. Le `COMPOSE_PROJECT_NAME` distinct est ce qui les isole sur un VPS mutualisé.
- **`*_SSH_KEY`** : copier l'intégralité du fichier clé, en-têtes `-----BEGIN … PRIVATE KEY-----` / `-----END … PRIVATE KEY-----` inclus.
- Les secrets ne sont **jamais** affichés dans les logs (masqués par GitHub) ; leur mise à jour ne s'applique qu'aux exécutions suivantes.
