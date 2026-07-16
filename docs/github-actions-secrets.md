# GitHub Actions — Secrets à configurer

Secrets à définir dans **Settings → Secrets and variables → Actions → Repository secrets**.

> Seuls les secrets consommés par `.github/workflows/ci.yml` sont listés. Les valeurs applicatives de prod
> (`APP_SECRET`, `MINIO_ROOT_*`, …) ne sont **pas** injectées par le pipeline : elles vivent dans le
> `${PROD_PATH}/.env` du serveur (cf. [configuration.md](configuration.md)).

---

## Secrets requis

| Secret | Job | Rôle | Exemple |
|---|---|---|---|
| `PROD_SSH_KEY` | `deploy` | Clé privée SSH (format PEM/OpenSSH) pour se connecter au serveur de prod. Charger la **clé privée** complète, la publique correspondante étant dans `~/.ssh/authorized_keys` côté serveur. | contenu de `id_ed25519` |
| `PROD_HOST` | `deploy` | Hôte cible (`ssh-keyscan` + destination SSH). | `league-of-data-base.fr` |
| `PROD_PATH` | `deploy` | Répertoire du projet compose sur le serveur (`cd` avant `docker compose`). | `/opt/lodb` |

## Secret optionnel

| Secret | Job | Rôle | Défaut |
|---|---|---|---|
| `PROD_SSH_USER` | `deploy` | Utilisateur SSH. | `root` (fallback `${{ secrets.PROD_SSH_USER || 'root' }}`) |

## Fourni automatiquement — ne pas créer

| Secret | Jobs | Note |
|---|---|---|
| `GITHUB_TOKEN` | `merge`, `build-and-push` | Injecté par GitHub. Nécessite `permissions: contents: write` (auto-merge dev→main) et `packages: write` (push GHCR), déjà déclarés dans le workflow. |

---

## Vérification rapide (GitHub CLI)

```bash
# Lister les secrets configurés
gh secret list

# Définir les secrets requis
gh secret set PROD_SSH_KEY < ~/.ssh/id_ed25519_prod
gh secret set PROD_HOST   --body "league-of-data-base.fr"
gh secret set PROD_PATH   --body "/opt/lodb"
gh secret set PROD_SSH_USER --body "deploy"   # optionnel
```

## Prérequis serveur (hors secrets)

- Clé **publique** SSH ajoutée dans `~/.ssh/authorized_keys` de `PROD_SSH_USER@PROD_HOST`.
- Docker Engine + Compose v2, login GHCR effectué (`docker login ghcr.io`).
- `${PROD_PATH}` = clone du repo (le job fait `git pull origin main`) avec `compose.yaml` et un `.env` de prod peuplé.
