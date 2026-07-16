# 🔐 GitHub Actions — Secrets

Secrets à configurer pour le pipeline `CI/CD` (`.github/workflows/ci.yml`).

**Où** : `Settings` → `Secrets and variables` → `Actions` → onglet `Repository secrets`.

## 🔁 Flux (promotion à deux étages, *build once*)

```
push dev  ─▶ test-php · test-go · test-js ─▶ merge-to-test (dev→test)
          ─▶ build-and-push (GHCR: :<sha> + :staging) ─▶ deploy-staging  (test.league-of-data-base.com)

merge test→main (manuel)  ─▶ push main
          ─▶ promote-and-deploy-prod : retag :staging→:prod (sans rebuild) ─▶ deploy prod (league-of-data-base.fr/.com)
```

- `test` est mis à jour automatiquement depuis `dev` et **ne déclenche jamais** le workflow (pas de boucle).
- `main` n'est atteint que par un **merge manuel `test → main`** : c'est le *gate* humain qui met en production.
- La prod **ne rebuild pas** : elle retague et déploie **exactement l'image validée en staging**
  (`:staging` courant → `:prod`). Corollaire : toujours passer par `test → main` — un push direct
  sur `main` déploierait l'image staging courante, pas le code poussé.

---

## 🧪 Tests (jobs `test-*`)

| Secret | Requis | Description |
|---|:---:|---|
| `ENV_TEST` | ✅ | Dotenv de test **complet**. Écrit dans `app/.env.test.local` avant PHPUnit / lints. |

---

## 🟡 Déploiement staging (job `deploy-staging`)

Serveur **dédié** au staging. Son `.env` doit fixer `IMAGE_TAG=staging` et
`CADDY_DOMAINS=test.league-of-data-base.com`.

| Secret | Requis | Description |
|---|:---:|---|
| `STAGING_SSH_KEY` | ✅ | Clé privée SSH (PEM complet) chargée dans `ssh-agent`. Clé publique dans les `authorized_keys` du serveur staging. |
| `STAGING_HOST` | ✅ | Hôte staging (IP ou FQDN). `ssh-keyscan` + connexions SSH. |
| `STAGING_PATH` | ✅ | Chemin absolu du projet sur le serveur (dossier des `compose.*.yaml`). Cible du `git pull origin test` et du `docker compose`. |
| `STAGING_SSH_USER` | ➖ | Utilisateur SSH. **Optionnel**, défaut `root`. |
| `ENV_STAGING` | ✅ | Dotenv staging **complet**. Poussé dans `${STAGING_PATH}/.env`. Doit inclure `REGISTRY=ghcr.io/<owner>/lodb`, `IMAGE_TAG=staging`, les secrets applicatifs (`APP_SECRET`, `MINIO_*`, `ADMIN_*`) **et** `CADDY_DOMAINS=test.league-of-data-base.com` + `ACME_EMAIL`. |

---

## 🚀 Déploiement production (job `promote-and-deploy-prod`)

Serveur **dédié** à la prod. Son `.env` doit fixer `IMAGE_TAG=prod` et les domaines apex.

| Secret | Requis | Description |
|---|:---:|---|
| `PROD_SSH_KEY` | ✅ | Clé privée SSH (PEM complet) chargée dans `ssh-agent`. Clé publique dans les `authorized_keys` du serveur prod. |
| `PROD_HOST` | ✅ | Hôte prod (IP ou FQDN). `ssh-keyscan` + connexions SSH. |
| `PROD_PATH` | ✅ | Chemin absolu du projet sur le serveur. Cible du `git pull origin main` et du `docker compose`. |
| `PROD_SSH_USER` | ➖ | Utilisateur SSH. **Optionnel**, défaut `root`. |
| `ENV_PROD` | ✅ | Dotenv prod **complet**. Poussé dans `${PROD_PATH}/.env`. Doit inclure `REGISTRY=ghcr.io/<owner>/lodb`, `IMAGE_TAG=prod`, les secrets applicatifs **et** `CADDY_DOMAINS=league-of-data-base.fr, league-of-data-base.com` + `ACME_EMAIL`. |

---

## 🔒 TLS / reverse-proxy (Caddy) — staging **et** prod

Les deux déploiements lancent la stack via `COMPOSE_FILE=compose.yaml:compose.deploy.yaml`
(overlay Caddy en frontal, **sans** `compose.override.yaml` dev). Caddy publie `80/443`,
obtient et renouvelle seul les certificats Let's Encrypt, et proxifie vers le nginx interne.
Clés à mettre dans `ENV_STAGING` / `ENV_PROD` :

| Variable | Staging | Prod |
|---|---|---|
| `CADDY_DOMAINS` | `test.league-of-data-base.com` | `league-of-data-base.fr, league-of-data-base.com` |
| `ACME_EMAIL` | contact Let's Encrypt | contact Let's Encrypt |

> ⚠️ **Ordre au premier déploiement** (par serveur) : les enregistrements DNS (A/AAAA)
> de chaque domaine doivent pointer vers l'hôte **avant** que le déploiement tourne, et les
> ports **80 + 443** doivent être ouverts — l'émission ACME (HTTP-01/TLS-ALPN) échoue sinon.
> Caddy réessaie tout seul une fois le DNS propagé.

### 🖥️ Prérequis serveur (one-shot, non automatisés — sur CHAQUE hôte)

À faire **une fois** sur staging **et** sur prod avant le premier push (ensuite 100 % auto) :

1. Docker Engine + plugin `docker compose` installés.
2. Repo cloné dans le `*_PATH` (le job fait `git pull`, pas `git clone`) ; staging suit `test`, prod suit `main`.
3. `docker login ghcr.io` persistant si les packages GHCR sont **privés** (PAT `read:packages`),
   sinon les rendre publics — sans ça `docker compose pull` échoue.
4. Ports **80/443** ouverts et DNS pointé (cf. ci-dessus).

---

## ⚙️ Secrets automatiques (aucune action requise)

| Secret | Description |
|---|---|
| `GITHUB_TOKEN` | Fourni automatiquement par GitHub Actions. Utilisé pour le merge `dev → test`, le push et le retag des images sur GHCR. **Ne pas créer manuellement.** |

---

## 📝 Notes

- **`ENV_*`** contiennent le fichier dotenv intégral (une variable par ligne), pas une valeur unique — coller le contenu complet du `.env` correspondant.
- **Staging et prod ne diffèrent que par leur `.env`** (`IMAGE_TAG`, `CADDY_DOMAINS`, secrets) : mêmes fichiers compose, même overlay Caddy.
- **`*_SSH_KEY`** : copier l'intégralité du fichier clé, en-têtes `-----BEGIN … PRIVATE KEY-----` / `-----END … PRIVATE KEY-----` inclus.
- Les secrets ne sont **jamais** affichés dans les logs (masqués par GitHub) ; leur mise à jour ne s'applique qu'aux exécutions suivantes.
