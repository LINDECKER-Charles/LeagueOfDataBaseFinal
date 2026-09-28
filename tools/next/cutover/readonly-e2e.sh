#!/usr/bin/env bash
# The E2E tests that write nothing, against any base URL (L8.2): the tests tagged @readonly in
# tests/LoDb.E2E/specs. They create no account, no build, no vote, no message and send no
# sign-in; they only read pages, the catalogue and the crawler files, so they may run on the
# production site once the domain points at the new stack.
#
#   tools/next/cutover/readonly-e2e.sh <base-url> [playwright arguments…]
#
# Needs the suite installed (npm ci --prefix tests/LoDb.E2E, then its browsers:install).
# Extra arguments go to Playwright, for instance --workers=2 or --reporter=line. Exit code of
# Playwright.
set -euo pipefail

if [ $# -lt 1 ]; then
  echo "Usage: $0 <base-url> [playwright arguments…]" >&2
  exit 2
fi
readonly BASE="$1"
shift

suite="$(cd "$(dirname "$0")/../../../tests/LoDb.E2E" && pwd)"
if [ ! -d "$suite/node_modules/@playwright/test" ]; then
  echo "The suite is not installed: npm ci --prefix tests/LoDb.E2E" >&2
  exit 2
fi

cd "$suite"
LODB_E2E_BASE_URL="$BASE" exec npx playwright test --grep @readonly "$@"
