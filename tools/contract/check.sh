#!/usr/bin/env bash
# Checks contract.sql on a throwaway database, without applying it anywhere else (L8.3).
#
#   tools/contract/check.sh
#
# 1. Starts a postgres:17-alpine container of its own (loopback, random port), runs
#    `migrate` on it (Baseline plus every migration of the rewrite), then writes one row the
#    way the legacy stack does in every table the contract touches.
# 2. Applies contract.sql in one transaction, with a session in Europe/Paris: the converted
#    instants must not depend on the session's time zone.
# 3. Asserts the result: legacy tables and users.roles gone, the seven timestamps in
#    timestamp(0) with time zone, same instants, typed default kept; then a second run
#    changes nothing, and `migrate` still has nothing to apply.
# 4. Asserts the guard: on a database without the rewrite's EF history, the script fails
#    and changes nothing.
#
# Needs Docker and the .NET SDK. Never touches lodb-dev, a slot or the legacy stack.
# Exit code 0 and "CONTRACT OK" when every assertion holds.
set -euo pipefail

readonly IMAGE=postgres:17-alpine
root="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
contract="$root/tools/contract/contract.sql"
scratch="lodb-contract-check-$$"
work="$(mktemp -d)"

cleanup() {
  docker rm -f "$scratch" >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

fail() {
  echo "FAILED: $*" >&2
  exit 1
}

# The second run's "does not exist, skipping" notices are expected: warnings and up only.
sql() {
  docker exec -i -e PGTZ=Europe/Paris -e PGOPTIONS='-c client_min_messages=warning' \
    "$scratch" psql -U lodb -v ON_ERROR_STOP=1 -At "$@"
}

migrate() {
  local connection="Host=127.0.0.1;Port=$port;Database=lodb;Username=lodb;Password=contract"
  ConnectionStrings__LoDb="$connection;Gss Encryption Mode=Disable" \
    dotnet run --project "$root/src/LoDb.Api" --no-launch-profile -- migrate >"$work/migrate.log" ||
    { cat "$work/migrate.log" >&2; fail "migrate"; }
  grep -o '[0-9]* migration(s) applied' "$work/migrate.log" | tail -n 1
}

schema() {
  docker exec "$scratch" pg_dump -U lodb --schema-only --no-owner --no-privileges -d "$1" |
    sed -E '/^(--|SET |SELECT pg_catalog\.set_config|\\restrict|\\unrestrict)/d; /^$/d'
}

# ── 1. A database at the rewrite's schema, with legacy rows ────────────────────────────
docker run -d --rm --name "$scratch" -p 127.0.0.1::5432 \
  -e POSTGRES_USER=lodb -e POSTGRES_PASSWORD=contract -e POSTGRES_DB=lodb "$IMAGE" >/dev/null
# Over TCP: the image's init server listens on the socket only, then restarts.
for _ in $(seq 1 60); do
  if docker exec "$scratch" pg_isready -q -h 127.0.0.1 -U lodb -d lodb; then break; fi
  sleep 1
done
port="$(docker port "$scratch" 5432/tcp | head -n 1 | sed -E 's/.*:([0-9]+)$/\1/')"
echo "migrate on an empty database: $(migrate)"

# 01:30 UTC on the night Paris skips 02:00-03:00: a conversion through the session's time
# zone would move it.
sql -d lodb -q <<'SQL'
INSERT INTO users (email, username, roles, created_at)
  VALUES ('legacy@example.test', 'legacy', '["ROLE_USER"]', '2026-03-29 01:30:00');
INSERT INTO builds (name, champion_id, game_version, runes, steps, share_token, created_at,
  updated_at, owner_id)
  SELECT 'b', 'Ahri', '16.18.1', '{}', '[]', 'token', '2026-03-29 01:30:00',
    '2026-03-29 01:30:01', id FROM users;
INSERT INTO build_votes (value, created_at, build_id, voter_id)
  SELECT 1, '2026-03-29 01:30:02', b.id, u.id FROM builds b, users u;
INSERT INTO contact_messages (category, email, message, created_at)
  VALUES ('other', 'legacy@example.test', 'm', '2026-03-29 01:30:03');
INSERT INTO donations (stripe_session_id, amount_cents, currency, created_at)
  VALUES ('cs_test', 500, 'eur', '2026-03-29 01:30:04');
INSERT INTO messenger_messages (body, headers, queue_name, created_at, available_at)
  VALUES ('{}', '{}', 'default', now(), now());
INSERT INTO reset_password_request (selector, hashed_token, requested_at, expires_at, user_id)
  SELECT 's', 'h', now(), now(), id FROM users;
SQL

instants() {
  sql -d lodb -c "SELECT to_char(created_at AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS')
    FROM users UNION ALL SELECT to_char(updated_at AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS')
    FROM builds UNION ALL SELECT to_char(created_at AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS')
    FROM donations"
}
# Before the contract the columns hold UTC wall clocks without a zone.
before="$(sql -d lodb -c "SELECT to_char(created_at, 'YYYY-MM-DD HH24:MI:SS') FROM users
  UNION ALL SELECT to_char(updated_at, 'YYYY-MM-DD HH24:MI:SS') FROM builds
  UNION ALL SELECT to_char(created_at, 'YYYY-MM-DD HH24:MI:SS') FROM donations")"

# ── 2. The contract ────────────────────────────────────────────────────────────────────
sql -d lodb --single-transaction -q <"$contract"
echo "contract.sql applied."

# ── 3. Its result ──────────────────────────────────────────────────────────────────────
for table in messenger_messages reset_password_request doctrine_migration_versions; do
  [ "$(sql -d lodb -c "SELECT to_regclass('$table') IS NULL")" = t ] || fail "$table remains"
done
[ -z "$(sql -d lodb -c "SELECT 1 FROM information_schema.columns
  WHERE table_name = 'users' AND column_name = 'roles'")" ] || fail "users.roles remains"
types="$(sql -d lodb -c "SELECT count(*) FROM information_schema.columns
  WHERE table_schema = 'public' AND data_type = 'timestamp with time zone'
    AND datetime_precision = 0 AND (table_name, column_name) IN (('build_votes', 'created_at'),
    ('builds', 'created_at'), ('builds', 'updated_at'), ('contact_messages', 'created_at'),
    ('contact_messages', 'handled_at'), ('donations', 'created_at'), ('users', 'created_at'))")"
[ "$types" = 7 ] || fail "$types of 7 columns in timestamp(0) with time zone"
[ -z "$(sql -d lodb -c "SELECT table_name || '.' || column_name FROM information_schema.columns
  WHERE table_schema = 'public' AND data_type = 'timestamp without time zone'")" ] ||
  fail "a column stays timestamp without time zone"
[ "$(instants)" = "$before" ] || fail "instants moved: $(instants | tr '\n' ' ')"
default="$(sql -d lodb -c "SELECT column_default FROM information_schema.columns
  WHERE table_name = 'contact_messages' AND column_name = 'handled_at'")"
[ "$default" = 'NULL::timestamp with time zone' ] || fail "handled_at default is $default"
echo "Legacy tables and users.roles dropped; 7 timestamps in timestamptz(0), same instants."

schema lodb >"$work/after-first.sql"
sql -d lodb --single-transaction -q <"$contract"
schema lodb >"$work/after-second.sql"
diff -u "$work/after-first.sql" "$work/after-second.sql" || fail "a second run changed the schema"
echo "Second run: schema unchanged. migrate afterwards: $(migrate)"

# ── 4. The guard ───────────────────────────────────────────────────────────────────────
sql -d postgres -q -c 'CREATE DATABASE guard'
sql -d guard -q -c 'CREATE TABLE messenger_messages (id bigint)'
if sql -d guard --single-transaction -q <"$contract" 2>"$work/guard.log"; then
  fail "the contract ran on a database without the rewrite's history"
fi
grep -q 'contract: the EF history lacks' "$work/guard.log" || { cat "$work/guard.log" >&2; fail guard; }
[ "$(sql -d guard -c "SELECT to_regclass('messenger_messages') IS NOT NULL")" = t ] ||
  fail "the guard let a change through"
echo "Guard: refused without the EF history, nothing changed."
echo "CONTRACT OK"
