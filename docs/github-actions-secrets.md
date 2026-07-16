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
| `ENV_PROD` | ✅ | Dotenv de prod **complet**. Poussé tel quel dans `${PROD_PATH}/.env` sur le serveur avant `docker compose up`. |

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
