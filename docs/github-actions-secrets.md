# 🔐 GitHub Actions — Secrets

Secrets à configurer pour le pipeline `CI/CD` (`.github/workflows/ci.yml`).

**Où** : `Settings` → `Secrets and variables` → `Actions` → onglet `Repository secrets`.

Le workflow se déclenche sur un `push` vers `dev` et enchaîne :
`test-php · test-go · test-js` → `merge` (dev → main) → `build-and-push` (GHCR) → `deploy` (SSH prod).

---

## 🚀 Déploiement production (job `deploy`)

Accès SSH au serveur de prod et publication du `.env` de production.

| Secret | Requis | Description |
|---|:---:|---|
| `PROD_SSH_KEY` | ✅ | Clé privée SSH (format PEM complet, en-têtes inclus) chargée dans `ssh-agent`. La clé publique correspondante doit être dans les `authorized_keys` du serveur. |
| `PROD_HOST` | ✅ | Hôte du serveur de prod (IP ou FQDN). Utilisé pour le `ssh-keyscan` et les connexions SSH. |
| `PROD_PATH` | ✅ | Chemin absolu du projet sur le serveur (dossier du `compose.yaml`). Cible du `git pull` et du `docker compose`. |
| `PROD_SSH_USER` | ➖ | Utilisateur SSH. **Optionnel**, défaut `root`. |
| `ENV_PROD` | ✅ | Dotenv de prod **complet**. Poussé tel quel dans `${PROD_PATH}/.env` sur le serveur avant `docker compose up`. Doit inclure `REGISTRY=ghcr.io/<owner>/lodb`, `IMAGE_TAG`, les secrets applicatifs (`APP_SECRET`, `MINIO_*`, `ADMIN_*`) **et** la conf TLS `CADDY_DOMAINS` + `ACME_EMAIL` (voir ci-dessous). |

### 🔒 TLS / reverse-proxy (Caddy)

Le job `deploy` lance la stack via `COMPOSE_FILE=compose.yaml:compose.prod.yaml`
(overlay prod = Caddy en frontal, **sans** `compose.override.yaml` dev). Caddy publie
`80/443`, obtient et renouvelle seul les certificats Let's Encrypt, et proxifie vers le
nginx interne. Clés à ajouter dans `ENV_PROD` :

| Variable | Exemple | Rôle |
|---|---|---|
| `CADDY_DOMAINS` | `league-of-data-base.fr, league-of-data-base.com` | Domaine(s) servis (liste séparée par virgules). Un certificat par domaine. |
| `ACME_EMAIL` | `admin@league-of-data-base.fr` | Contact Let's Encrypt (expiration/incidents). |

> ⚠️ **Ordre au premier déploiement** : les enregistrements DNS (A/AAAA) de chaque
> domaine doivent pointer vers `PROD_HOST` **avant** que le déploiement tourne, et les
> ports **80 + 443** doivent être ouverts (pare-feu / groupe de sécurité) — l'émission
> ACME (HTTP-01/TLS-ALPN) échoue sinon. Caddy réessaie tout seul une fois le DNS propagé.

### 🖥️ Prérequis serveur (one-shot, non automatisés)

À faire **une fois** sur `PROD_HOST` avant le premier push (le reste est ensuite 100 % auto) :

1. Docker Engine + plugin `docker compose` installés.
2. Repo cloné dans `PROD_PATH` (le job fait `git pull`, pas `git clone`).
3. `docker login ghcr.io` persistant si les packages GHCR sont **privés** (PAT `read:packages`),
   sinon les rendre publics — sans ça `docker compose pull` échoue.
4. Ports **80/443** ouverts et DNS pointé (cf. ci-dessus).

---

## 🧪 Tests (job `test-php`)

| Secret | Requis | Description |
|---|:---:|---|
| `ENV_TEST` | ✅ | Dotenv de test **complet**. Écrit dans `app/.env.test.local` avant l'exécution de PHPUnit / des lints. |

---

## ⚙️ Secrets automatiques (aucune action requise)

| Secret | Description |
|---|---|
| `GITHUB_TOKEN` | Fourni automatiquement par GitHub Actions. Utilisé pour le merge `dev → main` (job `merge`) et le push des images sur GHCR (job `build-and-push`). **Ne pas créer manuellement.** |

---

## 📝 Notes

- **`ENV_PROD` / `ENV_TEST`** contiennent le fichier dotenv intégral (une variable par ligne), pas une valeur unique — coller le contenu complet du `.env` correspondant.
- **`PROD_SSH_KEY`** : copier l'intégralité du fichier clé, y compris les lignes `-----BEGIN … PRIVATE KEY-----` / `-----END … PRIVATE KEY-----`.
- Les secrets ne sont **jamais** affichés dans les logs (masqués par GitHub) ; leur mise à jour ne s'applique qu'aux exécutions suivantes.
