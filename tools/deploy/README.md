# tools/deploy

Tools of the deploy job (`.github/workflows/_deploy.yml`), run on the host of a served
environment, and their tests, run by the `workflows` job of `.github/workflows/ci.yml`.

## Secrets as files

```sh
bash tools/deploy/split-env.sh ENV_FILE SECRETS_DIR < dotenv   # what the job runs on the host
bash tools/deploy/split-env.sh --keys                          # the secret keys
bash tools/deploy/split-env.sh --generated                     # the secrets drawn on the host
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

The keys of `GENERATED_KEYS` (the password of the API's database role, `LODB_APP_DB_PASSWORD`)
never come from the dotenv, which is refused when it sets one: the first run draws each one
with `openssl rand` into its file, every later run keeps it, and no copy exists outside the
host. Deleting the file draws a new value at the next deploy, which `migrate` sets on the role
before `up` recreates the API.

## Maintenance page

```sh
bash tools/deploy/maintenance.sh start .deploy/maintenance   # pages answer the maintenance page
bash tools/deploy/maintenance.sh end .deploy/maintenance     # the site again, window measured
```

The job turns the page on right before the first step that stops a container (`up`, or the
`down` of a stack network moved to its subnet) and off once the new stack passes its probes,
before the smoke tests ask for its pages. `compose.deploy.yaml` mounts the folder read-only in
nginx (`/srv/maintenance`), which answers every page with the page of `docker/nginx/maintenance`
while the switch is there (`server.d/maintenance.conf`).

| File | Written by | Content |
| --- | --- | --- |
| `active.json` | `start`, removed by `end` | the switch, and what the page polls: `{"startedAt":<Unix seconds>,"expectedSeconds":<seconds>}` |
| `last-duration` | `end` | the window's length in seconds, which the next `start` announces as `expectedSeconds` |

Without `last-duration` (the first deploy) or with one unreadable, `expectedSeconds` is left out
and the page shows the usual duration, 10 to 15 minutes. The measure lives on the host, never on
GitHub. A step that fails while the page is on leaves it there, since the stack may not serve:
the job says so (`::error` "Maintenance page left on"), and `rm .deploy/maintenance/active.json`
turns it off once the stack serves again. Both files are replaced by a rename, mode 644 in a
folder of mode 755: nginx reads them as its own user.

## Tests

```sh
node --test tools/deploy/test/*.test.mjs
```

`compose-secrets.test.mjs` renders the overlay with `docker compose config`, and
`split-env-compose.test.mjs` checks the script's values against Compose's own reading of the
same dotenv (both skipped without Docker). `maintenance.test.mjs` turns the maintenance switch in
a folder shaped like the host's. On a local stack, `stack-check/check-secret-files.sh` proves that Postgres, migrate and
the API read their secrets from files that win over the environment (header of the script).
