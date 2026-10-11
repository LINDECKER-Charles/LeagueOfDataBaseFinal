# tools/deploy

Tools of the deploy job (`.github/workflows/_deploy.yml`), run on the host of a served
environment, and their tests, run by the `workflows` job of `.github/workflows/ci.yml`.

## Secrets as files

```sh
bash tools/deploy/split-env.sh ENV_FILE SECRETS_DIR < dotenv   # what the job runs on the host
bash tools/deploy/split-env.sh --keys                          # the secret keys
```

The job pushes the environment's dotenv (secret `ENV_STAGING` or `ENV_PROD`) to
`.deploy/incoming.env`, then, once the checkout is at the deployed revision, splits it into a
scratch folder, which it checks against the target, and only then into place (a container
that restarts mounts the secret files as they are then):

| Output | Content |
| --- | --- |
| `.deploy/secrets/<key>` | one file per secret key, mode 444 in a folder of mode 700; empty when the dotenv leaves the key unset (Docker would mount a missing file as a folder) |
| `.env` (mode 600) | every other line, as it came, then `LODB_SECRETS_REVISION` |

`compose.deploy.yaml` mounts each file in `/run/secrets` of the services that read it: the API
finds its settings there by name (key-per-file, `LoDb__Billing__StripeSecretKey`…), Postgres
its password through `POSTGRES_PASSWORD_FILE`. No secret is then in a container's environment,
which `docker inspect` shows. `LODB_SECRETS_REVISION` goes up by one when a file changes; it
labels the API, which Compose then recreates: the API reads its files at start-up only.

A value is read as Compose reads a dotenv: unquoted (leading blanks dropped, then an inline
comment from a space followed by `#`), single-quoted (literal) or double-quoted. The script
refuses, before writing anything and without printing a value, a `$` outside single quotes
(Compose would interpolate it), a `\` inside double quotes or before a closing quote (Compose
reads `\'` as an escaped quote), a quote left open, and a secret written `KEY: value`.

A new secret goes to `SECRET_KEYS` **and** to the `secrets` of `compose.deploy.yaml`;
`test/compose-secrets.test.mjs` checks that they agree.

## Tests

```sh
node --test tools/deploy/test/*.test.mjs
```

`compose-secrets.test.mjs` renders the overlay with `docker compose config`, and
`split-env-compose.test.mjs` checks the script's values against Compose's own reading of the
same dotenv (both skipped without Docker). On a local stack, `stack-check/check-secret-files.sh` proves that Postgres, migrate and
the API read their secrets from files that win over the environment (header of the script).
