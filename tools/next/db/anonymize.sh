#!/usr/bin/env bash
# Anonymizes a dump of the LoDb database, for next and for local trials (plan, L1.4).
#
#   LODB_ANON_PASSWORD='…' tools/next/db/anonymize.sh <input> <output>
#
# <input>   a pg_dump of the database in a pg_restore format (pg_dump -Fc, the usual one).
# <output>  the anonymized copy, custom format, a new file. Restore it with
#           pg_restore --no-owner --no-privileges -d <database> <output>
#
# Everything happens in a throwaway postgres:17-alpine container without any network: the
# input is copied in and restored, anonymize.sql runs in one transaction, the result is
# dumped, and the container is removed with its data. Every account with a password gets
# LODB_ANON_PASSWORD (bcrypt); for a dump that goes to next, pick a strong one and share
# it like a secret, since it opens every account there. Row counts are printed before and
# after: only the queues, the tokens and the key ring change (emptied): messenger_messages
# and reset_password_request, then email_outbox, identity_user_tokens and
# data_protection_keys once the new stack has migrated the database (lot 4).
set -euo pipefail

readonly IMAGE=postgres:17-alpine

if [ $# -ne 2 ]; then
  echo "Usage: LODB_ANON_PASSWORD='…' $0 <input dump> <output dump>" >&2
  exit 2
fi
input="$1"
output="$2"
if [ ! -f "$input" ]; then
  echo "No such dump: $input" >&2
  exit 2
fi
if [ -e "$output" ]; then
  echo "$output already exists: the output is always a new file." >&2
  exit 2
fi
if [ -z "${LODB_ANON_PASSWORD:-}" ]; then
  echo "Set LODB_ANON_PASSWORD, the password every account of the copy gets." >&2
  exit 2
fi

here="$(cd "$(dirname "$0")" && pwd)"
container="lodb-anonymize-$$"
work="$(mktemp -d)"
cleanup() {
  docker rm -f "$container" >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

psql_lodb() {
  docker exec -i "$container" psql -U lodb -d lodb -q -v ON_ERROR_STOP=1 "$@"
}

# Exact counts of every table, one "table count" line each, in the order join expects.
count_rows() {
  psql_lodb -At <<'SQL'
SELECT format('%s %s', c.relname, (xpath('/row/n/text()', query_to_xml(
    format('SELECT count(*) AS n FROM %I', c.relname), false, true, '')))[1])
FROM pg_class c
WHERE c.relnamespace = current_schema()::regnamespace
  AND c.relkind IN ('r', 'p') AND NOT c.relispartition
ORDER BY c.relname COLLATE "C";
SQL
}

docker run -d --name "$container" --network none \
  -e POSTGRES_USER=lodb -e POSTGRES_PASSWORD=anonymize -e POSTGRES_DB=lodb \
  "$IMAGE" >/dev/null
# Over TCP: the image's init server listens on the socket only, then restarts.
for _ in $(seq 1 60); do
  if docker exec "$container" pg_isready -q -h 127.0.0.1 -U lodb -d lodb; then break; fi
  sleep 1
done

docker cp "$input" "$container:/tmp/input.dump" >/dev/null
if ! docker exec "$container" pg_restore --list /tmp/input.dump >/dev/null 2>&1; then
  echo "$input is not in a pg_restore format: dump it with pg_dump -Fc." >&2
  exit 1
fi
docker exec "$container" pg_restore -U lodb -d lodb --no-owner --no-privileges \
  --exit-on-error /tmp/input.dump
docker exec "$container" rm /tmp/input.dump
count_rows >"$work/before"

# bcrypt by pgcrypto, in the container's maintenance database: the copy gets no extension.
hash="$(echo "SELECT crypt(:'password', gen_salt('bf', 12));" \
  | docker exec -i -e LODB_ANON_PASSWORD "$container" sh -c \
    'psql -U lodb -d postgres -q -At -v ON_ERROR_STOP=1 -v password="$LODB_ANON_PASSWORD" \
      -c "CREATE EXTENSION IF NOT EXISTS pgcrypto" -f -')"

psql_lodb --single-transaction \
  -c "SET lodb.anon_password_hash = '$hash'" -f - <"$here/anonymize.sql"

echo "Rows (table before after):"
count_rows | LC_ALL=C join "$work/before" - | sed 's/^/  /'

docker exec "$container" pg_dump -U lodb -Fc -f /tmp/output.dump lodb
docker cp "$container:/tmp/output.dump" "$output" >/dev/null
echo "Anonymized copy written to $output."
