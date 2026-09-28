#!/usr/bin/env bash
# Smoke tests of the new stack at any base URL (L8.2): the window's first check once the
# domain points at it, and the rehearsal's. GET requests only, no account, no cookie kept.
#
#   tools/cutover/smoke.sh [base-url] [api-base-url]
#
# base-url      the site, http://localhost:18080 by default (a slot passes its own nginx;
#               the window, https://league-of-data-base.com).
# api-base-url  where /v1 answers, base-url by default (the window: https://api.…).
#
# The catalogue must hold a version: /api/meta names the latest one, whose Ahri page and
# first sitemap are then asked for. Needs curl and jq. One line per check; exit code 1 when
# any check fails.
set -euo pipefail

readonly BASE="${1:-http://localhost:18080}"
readonly API_BASE="${2:-$BASE}"
readonly TIMEOUT=60

failures=0
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# Status and Location of one GET, redirects not followed: "<status> <location>".
probe() {
  curl -sS -o "$work/body" -D "$work/headers" --max-time "$TIMEOUT" \
    -H 'Accept-Language: en' "$1" >/dev/null 2>"$work/error" || true
  local status location
  status="$(head -n 1 "$work/headers" 2>/dev/null | awk '{ print $2 }')"
  location="$({ grep -i '^location:' "$work/headers" || true; } | head -n 1 |
    cut -d: -f2- | sed -E 's/^ +//; s/\r$//')"
  echo "${status:-none} ${location}"
}

# One check: path, expected status, then an optional expected Location.
check() {
  local url="$1" status="$2" location="${3:-}"
  local answer actual_status actual_location
  answer="$(probe "$url")"
  actual_status="${answer%% *}"
  actual_location="${answer#* }"
  if [ "$actual_status" != "$status" ]; then
    echo "FAIL $url: $actual_status, expected $status $(cat "$work/error")"
    failures=$((failures + 1))
  elif [ -n "$location" ] && [ "$actual_location" != "$location" ]; then
    echo "FAIL $url: Location '$actual_location', expected '$location'"
    failures=$((failures + 1))
  else
    echo "ok   $status $url${location:+ -> $location}"
  fi
}

# One check of a body: path, then an extended regex the body must match.
contains() {
  local url="$1" text="$2"
  probe "$url" >/dev/null
  if grep -qE -- "$text" "$work/body" 2>/dev/null; then
    echo "ok   $url contains $text"
  else
    echo "FAIL $url: no $text in the body"
    failures=$((failures + 1))
  fi
}

check "$BASE/healthz" 200
check "$BASE/api/meta" 200
latest="$(jq -r '.latest // empty' "$work/body" 2>/dev/null || true)"
if [ -z "$latest" ]; then
  echo "FAIL $BASE/api/meta: no latest version, the catalogue is empty"
  exit 1
fi
echo "     latest version $latest"

# Pages, server-rendered, then the private ones rendered in the browser.
check "$BASE/" 302 /en/
for path in /en/ /fr/ /ar/ /en/champions /en/champions/Ahri /en/items /en/runes \
  /en/summoners /en/trends /en/about /en/faq /en/developers /en/donate /en/changelog \
  /en/legal/privacy /en/account/login; do
  check "$BASE$path" 200
done
# The server output may be minified: attribute quotes are optional.
contains "$BASE/en/champions/Ahri" 'rel="?canonical"?'
check "$BASE/en/nowhere" 404

# Crawler files and the former site's URLs.
check "$BASE/robots.txt" 200
check "$BASE/sitemap.xml" 200
check "$BASE/sitemaps/en/latest.xml" 200
check "$BASE/sitemaps/latest.xml" 301 /sitemap.xml
check "$BASE/champions" 301 /en/champions
check "$BASE/objects?lang=fr_FR" 301 /fr/items
check "$BASE/about" 301 /en/about
check "$BASE/login" 301 /en/account/login

# Contracts: the share links, the public API and the blobs keep their paths.
check "$BASE/b/ffffffffffffffffffffffff" 404
check "$API_BASE/v1/usage" 401
check "$API_BASE/v1/champions/Ahri/builds" 401

if [ "$failures" -gt 0 ]; then
  echo "$failures smoke check(s) failed on $BASE."
  exit 1
fi
echo "Every smoke check passes on $BASE."
