#!/usr/bin/env bash
# The old site after a rollback (plan-migration.md, cutover step 5; L8.2): its key pages
# still answer on the schema the new stack migrated. The sign-in and the /v1/usage of a key
# are accounts.mjs legacy-stack's.
#
#   tools/next/cutover/legacy-smoke.sh [site-url] [go-api-url]
#
# Defaults: the local legacy stack, http://localhost:8080 and http://localhost:8090. GET
# requests only. One line per check; exit code 1 when any check fails.
set -euo pipefail

readonly SITE="${1:-http://localhost:8080}"
readonly API="${2:-http://localhost:8090}"

failures=0

check() {
  local url="$1" expected="$2" status
  status="$(curl -sS -o /dev/null -w '%{http_code}' --max-time 120 "$url" 2>/dev/null || true)"
  if [ "$status" = "$expected" ]; then
    echo "ok   $status $url"
  else
    echo "FAIL $url: ${status:-no answer}, expected $expected"
    failures=$((failures + 1))
  fi
}

# Pages that read the database (trends, a profile) as well as the catalogue ones.
for path in / /champions /champion/Ahri /objects /object/1004 /runes /rune/Domination \
  /summoners /summoner/SummonerFlash /trends /about /faq /developers /donate /login \
  /register /sitemap.xml; do
  check "$SITE$path" 200
done
check "$SITE/profile" 302
check "$SITE/b/ffffffffffffffffffffffff" 404
check "$API/healthz" 200
check "$API/v1/usage" 401

if [ "$failures" -gt 0 ]; then
  echo "$failures check(s) failed on the old site."
  exit 1
fi
echo "The old site answers on this schema."
