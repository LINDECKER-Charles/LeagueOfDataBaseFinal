# SECRET_CONFIG (template)

Copy this file to `SECRET_CONFIG.md` (git-ignored) and fill in the real values.
It is the single reference for every secret/credential the stack and pipeline need.

> Never commit real secrets. `SECRET_CONFIG.md` and `.env` are git-ignored.

---

## 1. Local development — repo-root `.env`

Copy `.env.example` → `.env` and set:

| Variable | Purpose | Example |
|---|---|---|
| `APP_SECRET` | Symfony CSRF/signing secret | `bin2hex(random_bytes(16))` |
| `MINIO_ROOT_USER` | MinIO admin user | `lodb` |
| `MINIO_ROOT_PASSWORD` | MinIO admin password (min 8 chars) | strong random |
| `MINIO_BUCKET` | Bucket for DDragon blobs | `ddragon` |
| `HTTP_PORT` / `MINIO_*_PORT` / `MAILPIT_UI_PORT` | Published dev ports | see `.env.example` |

Start dev: `docker compose up -d --build` → app on `http://localhost:${HTTP_PORT}`,
MinIO console `http://localhost:${MINIO_CONSOLE_PORT}`, Mailpit `http://localhost:${MAILPIT_UI_PORT}`.

## 2. CI/CD — GitHub Actions repository secrets

| Secret | Used by | Notes |
|---|---|---|
| `GITHUB_TOKEN` | build/push images to GHCR | provided automatically; needs `packages: write` |
| `PROD_SSH_KEY` | deploy job SSH | private key for `root@league-of-data-base.fr` |
| `PROD_HOST` | deploy target host | e.g. `league-of-data-base.fr` |
| `PROD_PATH` | compose project dir on server | e.g. `/opt/lodb` |
| `PROD_APP_SECRET` | injected into prod `.env` | 32-hex |
| `PROD_MINIO_ROOT_USER` | prod MinIO user | — |
| `PROD_MINIO_ROOT_PASSWORD` | prod MinIO password | strong |

## 3. Production server prerequisites

- Docker Engine + Compose v2 installed.
- Logged in to GHCR: `echo $GHCR_PAT | docker login ghcr.io -u <user> --password-stdin`.
- A `${PROD_PATH}/.env` populated from section 1 values (prod-grade secrets).
- `compose.yaml` present at `${PROD_PATH}` (checked out or copied by the deploy job).
- Deploy = `docker compose pull && docker compose up -d`.
