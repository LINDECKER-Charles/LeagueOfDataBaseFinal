#!/usr/bin/env bash
# Writes hashes.json: password hashes made by PHP 8.5 the way the legacy stack makes them,
# which the .NET hasher must read (plan, L4.1).
#
#   tests/fixtures/hashes/generate.sh
#
# generate.php runs in a throwaway php:8.5-cli container without any network; the
# passwords are those of cases.php. The hashes are salted: every run rewrites the file,
# so commit it only with a change of generate.php or cases.php. verify.php, the reverse
# check, runs from the tests in the same image.
set -euo pipefail

readonly IMAGE=php:8.5-cli

here="$(cd "$(dirname "$0")" && pwd)"
docker run --rm --network none -v "$here:/fixtures:ro" "$IMAGE" \
  php /fixtures/generate.php >"$here/hashes.json.tmp"
mv "$here/hashes.json.tmp" "$here/hashes.json"
echo "Wrote $here/hashes.json."
