#!/usr/bin/env bash
# Acceptance of the URL grammar (plan, L3.1; ADR 0005): asks a started lodb-dev stack for
# each URL of the table below, without following redirects, and checks the status, the
# Location, the Cache-Control class and the X-Robots-Tag that the SSR server answers with.
#
#   tools/routing/check-urls.sh [base-url]
#
# base-url defaults to the integration stack (http://localhost:18080); a slot passes its own
# nginx, for instance http://localhost:18180. The catalogue rows need a version ingested:
# the script first waits for the patch watch to ingest one (LODB_ROUTING_WAIT seconds, 600
# by default), then asks the API through the same nginx for the item 1036 in the latest
# version and in the one before it, which ingests them on demand if needed.
#
# Needs curl and jq. Prints one line per check; exit code 1 when any check fails.
set -euo pipefail

readonly BASE="${1:-http://localhost:18080}"
readonly WAIT="${LODB_ROUTING_WAIT:-600}"
readonly POLL=5
readonly ITEM=1036
readonly MISSING_ITEM=999999

# Cache-Control of each response class (core/routing/response/cache-control.ts). A case
# rather than an associative array: macOS still ships bash 3.2.
cache_of() {
  case "$1" in
    latest) echo 'public, max-age=0, s-maxage=300, stale-while-revalidate=3600' ;;
    archived) echo 'public, max-age=3600, s-maxage=604800' ;;
    transient) echo 'public, max-age=0, s-maxage=60' ;;
    private) echo 'private, no-store' ;;
  esac
}

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
failures=0

# Fetches a URL of the stack without following redirects; its headers land in $work/headers.
fetch() {
  local path="$1" language="${2:-}"
  local args=(-sS -o /dev/null -D "$work/headers" --max-time 60)
  if [ -n "$language" ]; then
    args+=(-H "Accept-Language: $language")
  fi
  curl "${args[@]}" "$BASE$path"
}

# Value of a response header, empty when absent.
header() {
  { grep -i "^$1:" "$work/headers" || true; } | head -n 1 | cut -d: -f2- |
    sed -E 's/^ +//; s/\r$//'
}

status() {
  head -n 1 "$work/headers" | awk '{ print $2 }'
}

# Compares one value; "-" expects the header to be absent.
expect() {
  local path="$1" what="$2" actual="$3" expected="$4"
  if [ "$expected" = "-" ]; then
    expected=""
  fi
  if [ "$actual" != "$expected" ]; then
    echo "FAIL $path: $what is '$actual', expected '$expected'"
    failures=$((failures + 1))
    return 1
  fi
}

# One row: path, status, cache class, X-Robots-Tag and Location ("-" when absent).
row() {
  local path="$1" code="$2" cache="$3" robots="$4" location="$5"
  fetch "$path"
  local ok=0
  expect "$path" status "$(status)" "$code" || ok=1
  expect "$path" Cache-Control "$(header Cache-Control)" "$(cache_of "$cache")" || ok=1
  expect "$path" X-Robots-Tag "$(header X-Robots-Tag)" "$robots" || ok=1
  expect "$path" Location "$(header Location)" "$location" || ok=1
  if [ "$ok" = 0 ] && [ "$location" = "-" ]; then
    echo "ok   $code $path"
  elif [ "$ok" = 0 ]; then
    echo "ok   $code $path -> $location"
  fi
}

# `/` answers a 302 to the negotiated locale; Vary lists Accept-Language (Express adds
# Accept, for the body of its redirect).
root() {
  local language="$1" locale="$2"
  fetch / "$language"
  local ok=0 label="/ ($language)"
  expect "$label" status "$(status)" 302 || ok=1
  expect "$label" Location "$(header Location)" "/$locale/" || ok=1
  expect "$label" Cache-Control "$(header Cache-Control)" "$(cache_of transient)" || ok=1
  if ! header Vary | grep -qi 'accept-language'; then
    echo "FAIL $label: Vary is '$(header Vary)', without Accept-Language"
    failures=$((failures + 1))
    ok=1
  fi
  if [ "$ok" = 0 ]; then
    echo "ok   302 $label -> /$locale/"
  fi
}

# A prerendered page comes from the build, with the ETag the SSR server gives static files.
prerendered() {
  fetch "$1"
  if [ -z "$(header ETag)" ]; then
    echo "FAIL $1: no ETag, the page was rendered on request instead of prerendered"
    failures=$((failures + 1))
  else
    echo "ok   prerendered $1"
  fi
}

meta() {
  curl -sS --max-time 30 "$BASE/api/meta"
}

# The canonical path of an item for a version, asked in en_US (the language of /en/).
canonical_path() {
  curl -sS --max-time 300 "$BASE/api/catalog/$1/en_US/items/$ITEM" | jq -er .canonicalPath
}

# ── The catalogue must hold a version ────────────────────────────────────────────────
deadline=$((SECONDS + WAIT))
until latest="$(meta | jq -er '.latest // empty' 2>/dev/null)"; do
  if [ "$SECONDS" -ge "$deadline" ]; then
    echo "No version ingested after ${WAIT}s: /api/meta has no latest version." >&2
    exit 1
  fi
  sleep "$POLL"
done
older="$(meta | jq -er --arg latest "$latest" '.versions as $v | $v[($v | index($latest)) + 1]')"
item="$(canonical_path "$latest")"
older_item="$(canonical_path "$older")"
echo "latest $latest ($item), older $older ($older_item)"

# ── `/` ──────────────────────────────────────────────────────────────────────────────
root 'fr-FR,fr;q=0.9,en;q=0.8' fr
root 'pt-BR,pt;q=0.9' pt
root 'zh-TW' zh-hant
root 'nl,sv' en
root '' en

# ── Pages without the catalogue ──────────────────────────────────────────────────────
row /en/ 200 latest - -
row /en 200 latest - -
row /ar/ 200 latest - -
row /fr/about 200 latest - -
row /ar/legal/cookies 200 latest - -
row /zh-hant/faq 200 latest - -
prerendered /fr/about
prerendered /en/legal/terms
row /en/trends 200 latest - -
row /en/u/faker 200 latest - -
row /en/developers 200 latest - -
row /en/donate 200 latest - -
row /b/Zx81kQ 200 latest noindex -

# ── Private pages: rendered in the browser, never stored, never indexed ──────────────
row /en/account/login 200 private noindex -
row /fr/account/reset-password/t0k3n 200 private noindex -
row /en/account/builds/42/edit 200 private noindex -
row /en/account/api 200 private noindex -
row /admin 200 private noindex -

# ── Real 404s, rendered in place ─────────────────────────────────────────────────────
row /xx/about 404 transient noindex -
row /xx/champions 404 transient noindex -
row /en/nowhere 404 transient noindex -
row /en/u 404 transient noindex -
row /b 404 transient noindex -
row "/en/$older" 404 transient noindex -
row /en/99.99.99/champions 404 transient noindex -
row "/en/items/$MISSING_ITEM" 404 transient noindex -

# ── Catalogue: the latest version on its short URLs, older ones pinned ───────────────
row /en/champions 200 latest - -
row "/fr/$older/runes" 200 archived - -
row "/en/$item" 200 latest - -
row "/en/$older/$older_item" 200 archived - -

# ── Canonical redirects ──────────────────────────────────────────────────────────────
row "/en/$latest/champions" 301 transient - /en/champions
row "/en/champions?version=$older" 301 transient - "/en/$older/champions"
row "/en/champions?version=$latest&lang=en_GB" 301 transient - "/en/champions?lang=en_GB"
row "/en/items/$ITEM" 301 transient - "/en/$item"
row "/en/items/$ITEM-wrong-slug" 301 transient - "/en/$item"
row "/en/$latest/$item" 301 transient - "/en/$item"
# Decided from /api/meta, before any fetch: the next hop fixes the slug.
row "/en/$latest/items/$ITEM" 301 transient - "/en/items/$ITEM"
row "/en/$older/items/$ITEM" 301 transient - "/en/$older/$older_item"
row "/en/$older/items/$MISSING_ITEM" 302 transient - "/en/$older/items"

if [ "$failures" -gt 0 ]; then
  echo "$failures check(s) failed."
  exit 1
fi
echo "Every URL answers as ADR 0005 says."
