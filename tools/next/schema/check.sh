#!/usr/bin/env bash
# Checks that the EF migrations reproduce the schema of the Doctrine migrations, and
# freezes that schema so that the tests compare without the legacy stack (plan, L1.4).
#
#   tools/next/schema/check.sh
#
# 1. Creates the empty database lodb_schema_ref on the legacy Postgres (project lodb),
#    applies the Doctrine migrations to it through the php container (-u www-data), reads
#    it, then drops it. The legacy stack's own database is never touched, and the stack is
#    neither started nor stopped.
# 2. Writes what the tests and `migrate` compare with:
#      tests/fixtures/schema/doctrine-schema.sql    normalized pg_dump --schema-only
#      tests/fixtures/schema/doctrine-versions.txt  rows of doctrine_migration_versions
#      src/LoDb.Infrastructure/Persistence/Baseline/doctrine-catalog.txt  (embedded)
# 3. Runs `migrate` (the API's sub-command) on a throwaway postgres:17-alpine container,
#    then compares its normalized dump with the Doctrine one, without the tables that only
#    the new stack has (NEW_TABLES). Exit code 0 and "IDENTICAL" when they match.
#
# Needs Docker, the legacy stack running (services php and postgres) and the .NET SDK.
# After a new Doctrine migration: add its EF mirror, run this script, commit the outputs.
set -euo pipefail

readonly REF_DB=lodb_schema_ref
readonly IMAGE=postgres:17-alpine
# Tables that exist only in the new stack: the EF history, then the tables of the lots.
readonly NEW_TABLES=(__EFMigrationsHistory ddragon_asset ddragon_version periodic_job)

root="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
fixtures="$root/tests/fixtures/schema"
baseline_dir="$root/src/LoDb.Infrastructure/Persistence/Baseline"
work="$(mktemp -d)"
scratch=""
ref_created=0

# pg_dump's header, settings and blank lines vary with the run and the version.
normalize() {
  sed -E '/^(--|SET |SELECT pg_catalog\.set_config|\\restrict|\\unrestrict)/d; /^$/d'
}

legacy_container() {
  docker ps \
    --filter label=com.docker.compose.project=lodb \
    --filter "label=com.docker.compose.service=$1" \
    --format '{{.Names}}' | head -n 1
}

cleanup() {
  if [ "$ref_created" = 1 ]; then
    docker exec "$pg" psql -U "$pg_user" -d postgres -q \
      -c "DROP DATABASE IF EXISTS $REF_DB" >/dev/null 2>&1 || true
  fi
  if [ -n "$scratch" ]; then
    docker rm -f "$scratch" >/dev/null 2>&1 || true
  fi
  rm -rf "$work"
}
trap cleanup EXIT

php="$(legacy_container php)"
pg="$(legacy_container postgres)"
if [ -z "$php" ] || [ -z "$pg" ]; then
  echo "The legacy stack (project lodb) must be running: services php and postgres." >&2
  exit 1
fi
pg_user="$(docker exec "$pg" printenv POSTGRES_USER)"
legacy_url="$(docker exec "$php" printenv DATABASE_URL)" || {
  echo "DATABASE_URL is not set in $php." >&2
  exit 1
}

# ── 1. The Doctrine schema, in a database of its own ───────────────────────────────────
exists="$(docker exec "$pg" psql -U "$pg_user" -d postgres -At \
  -c "SELECT 1 FROM pg_database WHERE datname = '$REF_DB'")"
if [ -n "$exists" ]; then
  echo "Database $REF_DB already exists: another run, or a leftover to drop by hand." >&2
  exit 1
fi
docker exec "$pg" psql -U "$pg_user" -d postgres -q -c "CREATE DATABASE $REF_DB"
ref_created=1

# Same server and credentials as the legacy application, another database.
ref_url="$(printf '%s' "$legacy_url" | sed -E "s#^([a-z0-9]+://[^/]*/)[^?]*#\1$REF_DB#")"
echo "Applying the Doctrine migrations to ${REF_DB}…"
docker exec -u www-data -e "DATABASE_URL=$ref_url" "$php" \
  php bin/console doctrine:migrations:migrate --no-interaction --quiet

# ── 2. What the tests and `migrate` compare with ───────────────────────────────────────
docker exec "$pg" pg_dump -U "$pg_user" --schema-only --no-owner --no-privileges "$REF_DB" \
  | normalize >"$fixtures/doctrine-schema.sql"
docker exec "$pg" psql -U "$pg_user" -d "$REF_DB" -At \
  -c 'SELECT version FROM doctrine_migration_versions ORDER BY version' \
  >"$fixtures/doctrine-versions.txt"
docker exec -i "$pg" psql -U "$pg_user" -d "$REF_DB" -At -v ON_ERROR_STOP=1 \
  <"$baseline_dir/schema-catalog.sql" >"$baseline_dir/doctrine-catalog.txt"
legacy_dump_version="$(docker exec "$pg" pg_dump --version)"
docker exec "$pg" psql -U "$pg_user" -d postgres -q -c "DROP DATABASE $REF_DB"
ref_created=0
echo "Doctrine: $(wc -l <"$fixtures/doctrine-versions.txt" | tr -d ' ') migrations," \
  "$(wc -l <"$baseline_dir/doctrine-catalog.txt" | tr -d ' ') catalog lines."

# ── 3. `migrate` on an empty database, compared with Doctrine ──────────────────────────
scratch="lodb-schema-check-$$"
docker run -d --rm --name "$scratch" -p 127.0.0.1::5432 \
  -e POSTGRES_USER=lodb -e POSTGRES_PASSWORD=schema-check -e POSTGRES_DB=lodb \
  "$IMAGE" >/dev/null
# Over TCP: the image's init server listens on the socket only, then restarts.
for _ in $(seq 1 60); do
  if docker exec "$scratch" pg_isready -q -h 127.0.0.1 -U lodb -d lodb; then break; fi
  sleep 1
done
port="$(docker port "$scratch" 5432/tcp | head -n 1 | sed -E 's/.*:([0-9]+)$/\1/')"

echo "Running migrate on an empty database…"
connection="Host=127.0.0.1;Port=$port;Database=lodb;Username=lodb;Password=schema-check"
if ! ConnectionStrings__LoDb="$connection;Gss Encryption Mode=Disable" \
  dotnet run --project "$root/src/LoDb.Api" --no-launch-profile -- migrate \
  >"$work/migrate.log"; then
  cat "$work/migrate.log" >&2
  exit 1
fi

excluded=()
for table in "${NEW_TABLES[@]}"; do
  excluded+=("--exclude-table=public.\"$table\"")
done
docker exec "$scratch" pg_dump -U lodb --schema-only --no-owner --no-privileges \
  "${excluded[@]}" lodb | normalize >"$work/migrated-schema.sql"
docker exec "$scratch" psql -U lodb -d lodb -At \
  -c 'SELECT version FROM doctrine_migration_versions ORDER BY version' \
  >"$work/migrated-versions.txt"

echo "pg_dump: $legacy_dump_version (Doctrine), $(docker exec "$scratch" pg_dump --version) (EF)"
echo "EF history: $(docker exec "$scratch" psql -U lodb -d lodb -At \
  -c 'SELECT string_agg(migration_id, $$, $$ ORDER BY migration_id) FROM "__EFMigrationsHistory"')"
echo "Excluded (new stack only): ${NEW_TABLES[*]}"
if diff -u "$fixtures/doctrine-schema.sql" "$work/migrated-schema.sql" \
  && diff -u "$fixtures/doctrine-versions.txt" "$work/migrated-versions.txt"; then
  echo "IDENTICAL: the EF migrations reproduce the Doctrine schema and history."
else
  echo "DIFFERENT: see the diff above (- Doctrine, + EF)." >&2
  exit 1
fi
