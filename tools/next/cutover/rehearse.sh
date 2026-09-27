#!/usr/bin/env bash
# Local rehearsal of the cutover, the exit criterion of lot 8 (L8.2): a copy of the legacy
# stack's database is migrated, the new stack serves it (smoke tests, 301, read-only E2E,
# existing accounts), then the legacy stack is started again on that same copy and still
# answers (key pages, sign-in, /v1/usage): the rollback is proven.
#
#   tools/next/cutover/rehearse.sh [--legacy-dir <dir>] [--slot 2] [--copy lodb_rehearsal]
#        [--anonymize] [--accounts-file <file>] [--historical 1] [--no-build]
#        [--skip-pre-ingest] [--skip-e2e] [--keep] [--stop-legacy]
#
# --legacy-dir     the checkout the legacy stack (project lodb) runs from; this repository by
#                  default. Its tracked files and its .env are never written: the copy is
#                  named through POSTGRES_DB in the environment of `docker compose`.
# --slot           the lodb-next slot the new stack runs on (1 or 2), from this repository.
# --copy           the database the copy goes to, next to the legacy one in its PostgreSQL;
#                  dropped and created again. Never the legacy database itself.
# --anonymize      passes the dump through tools/next/db/anonymize.sh; every account of the
#                  copy then has LODB_ANON_PASSWORD (a random one when unset), and five of them
#                  are signed in as well.
# --accounts-file  accounts of the copy whose password is known, `<identifier>:<password>`
#                  per line, checked with the seeded ones (accounts.mjs).
# --historical     version sitemaps followed by the 301 check beyond the primary one.
# --keep           keeps the slot and the copy; by default the slot is removed (down -v) and
#                  the copy dropped.
# --stop-legacy    stops the legacy stack at the end (docker compose stop).
#
# Steps: copy, seeded accounts, migrate (twice: the second applies nothing), pre-ingestion
# (ingest --latest 3 --languages all), new stack up, smoke, 301, read-only E2E, accounts on
# the new stack, new stack stopped, legacy stack on the copy, its smoke and accounts, legacy
# stack back on its own database. Whatever happens, the legacy stack is put back on its own
# database. Needs docker, node, jq, curl, and the E2E suite installed in this repository.
# Prints a summary of the steps; exit code 0 when every step passes.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../.." && pwd)"
legacy_dir="$repo"
slot=2
copy=lodb_rehearsal
anonymize=0
accounts_file=""
historical=1
build=1
pre_ingest=1
e2e=1
keep=0
stop_legacy=0

while [ $# -gt 0 ]; do
  case "$1" in
    --legacy-dir) legacy_dir="$(cd "$2" && pwd)"; shift 2 ;;
    --slot) slot="$2"; shift 2 ;;
    --copy) copy="$2"; shift 2 ;;
    --anonymize) anonymize=1; shift ;;
    --accounts-file) accounts_file="$(cd "$(dirname "$2")" && pwd)/$(basename "$2")"; shift 2 ;;
    --historical) historical="$2"; shift 2 ;;
    --no-build) build=0; shift ;;
    --skip-pre-ingest) pre_ingest=0; shift ;;
    --skip-e2e) e2e=0; shift ;;
    --keep) keep=1; shift ;;
    --stop-legacy) stop_legacy=1; shift ;;
    *) echo "Unknown option $1 (see the header of $0)." >&2; exit 2 ;;
  esac
done

case "$slot" in
  1|2) ;;
  *) echo "--slot is 1 or 2: the integration stack is never rehearsed on." >&2; exit 2 ;;
esac
if ! [[ "$copy" =~ ^[a-z_][a-z0-9_]*$ ]]; then
  echo "--copy must be a plain database name." >&2
  exit 2
fi

readonly LEGACY_POSTGRES=lodb-postgres-1
readonly LEGACY_SITE=http://localhost:8080
readonly LEGACY_API=http://localhost:8090
# The slots' ports (docs/guides/dev-next.md, Emplacements).
if [ "$slot" = 1 ]; then
  export LODB_NEXT_HTTP_PORT=18180 LODB_NEXT_API_PORT=18181 LODB_NEXT_SSR_PORT=18182
  export LODB_NEXT_PG_PORT=15532 LODB_NEXT_MAIL_PORT=18125
else
  export LODB_NEXT_HTTP_PORT=18280 LODB_NEXT_API_PORT=18281 LODB_NEXT_SSR_PORT=18282
  export LODB_NEXT_PG_PORT=15632 LODB_NEXT_MAIL_PORT=18225
fi
readonly BASE="http://localhost:$LODB_NEXT_HTTP_PORT"
readonly NEXT=(-p "lodb-next-e$slot" -f compose.next.yaml -f compose.next.override.yaml
  -f tools/next/cutover/compose.rehearsal.yaml)

work="$(mktemp -d)"
legacy_on_copy=0
summary=()
failures=0

log() { printf '\n== %s\n' "$*"; }

next_compose() { (cd "$repo" && docker compose "${NEXT[@]}" "$@"); }

legacy_compose() { (cd "$legacy_dir" && docker compose "$@"); }

psql_legacy() {
  docker exec -i "$LEGACY_POSTGRES" psql -U "$pg_user" -v ON_ERROR_STOP=1 -At "$@"
}

# The legacy stack on the database named by $1 (empty: its own, from its .env). Its dev nginx
# resolves php once, at start: once php is recreated on another database, nginx restarts too,
# or it answers 502 from the old address.
legacy_on() {
  if [ -n "$1" ]; then
    POSTGRES_DB="$1" legacy_compose up -d --wait
  else
    legacy_compose up -d --wait
  fi
  legacy_compose restart nginx
  local status=""
  for _ in $(seq 1 60); do
    status="$(curl -s -o /dev/null -w '%{http_code}' --max-time 30 "$LEGACY_SITE/" || true)"
    if [ "$status" = 200 ]; then
      return 0
    fi
    sleep 2
  done
  echo "The legacy site answers $status on /." >&2
  return 1
}

restore_legacy() {
  if [ "$legacy_on_copy" = 1 ]; then
    log "Legacy stack back on its own database"
    legacy_on "" && legacy_on_copy=0
  fi
  if [ -n "${source_db:-}" ]; then
    docker exec lodb-php-1 printenv DATABASE_URL | grep -q "/$source_db?"
  fi
}

finish() {
  local status=$?
  restore_legacy || echo "WARNING: the legacy stack could not be put back on its database." >&2
  if [ "$keep" = 0 ]; then
    next_compose down -v --rmi local >/dev/null 2>&1 || true
    psql_legacy -d postgres -c "DROP DATABASE IF EXISTS $copy WITH (FORCE)" >/dev/null 2>&1 || true
  fi
  if [ "$stop_legacy" = 1 ]; then
    legacy_compose stop >/dev/null 2>&1 || true
  fi
  rm -rf "$work"
  exit "$status"
}
trap finish EXIT

# One step: its name, then its command. A failed step is recorded and the rehearsal goes on.
step() {
  local name="$1" started=$SECONDS status=0
  shift
  log "$name"
  "$@" || status=$?
  local seconds=$((SECONDS - started))
  if [ "$status" = 0 ]; then
    summary+=("ok    ${seconds}s  $name")
  else
    summary+=("FAIL  ${seconds}s  $name (code $status)")
    failures=$((failures + 1))
  fi
}

# A step the next ones depend on: a failure stops the rehearsal.
required() {
  step "$@"
  if [ "${summary[${#summary[@]}-1]:0:4}" = FAIL ]; then
    print_summary
    exit 1
  fi
}

print_summary() {
  log "Summary"
  printf '%s\n' "${summary[@]}"
}

# ── Preconditions ────────────────────────────────────────────────────────────────────
for tool in docker node jq curl; do
  command -v "$tool" >/dev/null || { echo "$tool is missing." >&2; exit 2; }
done
running_from="$(docker inspect -f '{{ index .Config.Labels "com.docker.compose.project.working_dir" }}' \
  "$LEGACY_POSTGRES" 2>/dev/null || true)"
if [ -n "$running_from" ] && [ "$running_from" != "$legacy_dir" ]; then
  echo "The legacy stack runs from $running_from, not $legacy_dir: it is not recreated from here." >&2
  exit 2
fi
if [ "$e2e" = 1 ] && [ ! -d "$repo/tests/LoDb.E2E/node_modules/@playwright/test" ]; then
  echo "The E2E suite is not installed: npm ci --prefix tests/LoDb.E2E (or --skip-e2e)." >&2
  exit 2
fi

log "Legacy stack on its own database"
legacy_on ""
pg_user="$(docker exec "$LEGACY_POSTGRES" printenv POSTGRES_USER)"
source_db="$(docker exec "$LEGACY_POSTGRES" printenv POSTGRES_DB)"
LODB_REHEARSAL_DB_PASSWORD="$(docker exec "$LEGACY_POSTGRES" printenv POSTGRES_PASSWORD)"
export LODB_REHEARSAL_DB_PASSWORD LODB_REHEARSAL_DB_NAME="$copy" LODB_REHEARSAL_DB_USER="$pg_user"
if [ "$copy" = "$source_db" ]; then
  echo "--copy names the legacy database itself ($source_db)." >&2
  exit 2
fi

# ── 1. The copy ──────────────────────────────────────────────────────────────────────
counts() {
  psql_legacy -d "$1" -F ' ' -c "SELECT 'users', count(*) FROM users UNION ALL
    SELECT 'builds', count(*) FROM builds UNION ALL SELECT 'api_keys', count(*) FROM api_keys"
}

copy_database() {
  docker exec "$LEGACY_POSTGRES" pg_dump -U "$pg_user" -Fc "$source_db" >"$work/source.dump"
  local dump="$work/source.dump"
  if [ "$anonymize" = 1 ]; then
    export LODB_ANON_PASSWORD="${LODB_ANON_PASSWORD:-$(openssl rand -hex 12)}"
    "$repo/tools/next/db/anonymize.sh" "$work/source.dump" "$work/anonymized.dump"
    dump="$work/anonymized.dump"
  fi
  psql_legacy -d postgres -c "DROP DATABASE IF EXISTS $copy WITH (FORCE)" -c "CREATE DATABASE $copy"
  docker exec -i "$LEGACY_POSTGRES" pg_restore -U "$pg_user" -d "$copy" --no-owner \
    --no-privileges --exit-on-error <"$dump"
  echo "Rows, $source_db then $copy:"
  paste <(counts "$source_db") <(counts "$copy")
}
copy_label="Copy $source_db into $copy"
if [ "$anonymize" = 1 ]; then
  copy_label="$copy_label, anonymized"
fi
required "$copy_label" copy_database

seed_accounts() {
  local file="$work/accounts"
  : >"$file"
  chmod 600 "$file"
  if [ -n "$accounts_file" ]; then
    cat "$accounts_file" >>"$file"
  fi
  if [ "$anonymize" = 1 ]; then
    psql_legacy -d "$copy" -c "SELECT coalesce(username, email) FROM users
      WHERE password IS NOT NULL AND NOT is_banned ORDER BY id LIMIT 5" |
      sed "s/\$/:$LODB_ANON_PASSWORD/" >>"$file"
  fi
  node "$here/accounts.mjs" seed --state "$work/state.json" --postgres "$LEGACY_POSTGRES" \
    --database "$copy" --pg-user "$pg_user" --accounts-file "$file"
}
required "Existing accounts of the copy" seed_accounts

# ── 2. Migrations, then the new stack on the copy ────────────────────────────────────
if [ "$build" = 1 ]; then
  required "Build the new stack (lodb-next-e$slot)" next_compose build
fi

migrate_twice() {
  next_compose run --rm migrate
  echo "-- again: nothing left to apply"
  next_compose run --rm migrate
  psql_legacy -d "$copy" -c 'SELECT migration_id FROM "__EFMigrationsHistory" ORDER BY 1'
}
required "migrate on $copy (Baseline marked, additive migrations)" migrate_twice

if [ "$pre_ingest" = 1 ]; then
  step "Pre-ingestion (ingest --latest 3 --languages all)" \
    "$here/pre-ingest.sh" --latest 3 -- "${NEXT[@]}"
fi

required "New stack up on $copy" next_compose up -d --wait
step "Smoke tests of the new stack" "$here/smoke.sh" "$BASE"
step "301 of the former sitemaps" node "$here/check-301.mjs" --base "$BASE" \
  --sitemap "$LEGACY_SITE/sitemap.xml" --historical "$historical" --json "$work/301.json"
if [ "$e2e" = 1 ]; then
  # One retry, as on CI: Playwright still lists a test that passed on retry as flaky.
  step "Read-only E2E (@readonly)" "$here/readonly-e2e.sh" "$BASE" --reporter=line --retries=1
fi
step "Existing accounts on the new stack, API key" node "$here/accounts.mjs" new-stack \
  --state "$work/state.json" --base "$BASE" --postgres "$LEGACY_POSTGRES" --database "$copy" \
  --pg-user "$pg_user"

# ── 3. Rollback: the new stack stops, the legacy stack serves the migrated copy ──────
required "New stack stopped (rollback)" next_compose stop nginx web-ssr api

legacy_on_copy_step() {
  legacy_on_copy=1
  legacy_on "$copy"
  docker exec lodb-php-1 printenv DATABASE_URL | grep -q "/$copy?" &&
    docker exec lodb-go-api-1 printenv DATABASE_URL | grep -q "/$copy\$" &&
    echo "php and go-api read $copy."
}
required "Legacy stack on $copy" legacy_on_copy_step
step "Legacy key pages on the migrated schema" "$here/legacy-smoke.sh" "$LEGACY_SITE" "$LEGACY_API"
step "Legacy sign-in and /v1/usage after the new stack" node "$here/accounts.mjs" legacy-stack \
  --state "$work/state.json" --base "$LEGACY_SITE" --api "$LEGACY_API"

step "Legacy stack back on $source_db" restore_legacy

print_summary
if [ "$failures" -gt 0 ]; then
  echo "$failures step(s) failed: the rehearsal does not pass."
  exit 1
fi
echo "The rehearsal passes: migrated copy served by the new stack, then by the legacy one."
