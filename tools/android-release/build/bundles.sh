#!/usr/bin/env bash
# Live update bundles of an Android release (ADR 0008), signed with the bundle key of the
# environment (LODB_LIVE_UPDATE_PRIVATE_KEY, tools/live-update):
#
#   bundles.sh --web src/LoDb.Web/dist/shell-store/browser --version X.Y.Z \
#     --minimum-native A.B.C --base-url https://github.com/<repo>/releases/download/<tag> \
#     --out bundles
#
# Writes to --out, each zip with its descriptor (.json, the values of client-policy publish):
#   lodb-bundle-X.Y.Z.zip                 the bundle of the release, id X.Y.Z
#   gate/lodb-bundle-X.Y.Z-gate-faulty    signed, but its page never starts the app: the gate
#                                         checks that the plugin rolls it back
#   gate/lodb-bundle-X.Y.Z-gate-tampered  the release bundle plus one file, under the release
#                                         signature: the gate checks that the plugin refuses it
# Only the release bundle is published; the gate removes its own after it ran.
set -euo pipefail

LIVE_UPDATE="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../live-update" && pwd)"
readonly LIVE_UPDATE

fail() {
  echo "bundles.sh: $*" >&2
  exit 1
}

web="" version="" minimum="" base_url="" out=""
while [ $# -gt 0 ]; do
  case "$1" in
    --web) web="$2"; shift 2 ;;
    --version) version="$2"; shift 2 ;;
    --minimum-native) minimum="$2"; shift 2 ;;
    --base-url) base_url="$2"; shift 2 ;;
    --out) out="$2"; shift 2 ;;
    *) fail "unknown argument '$1'" ;;
  esac
done
for required in web version minimum base_url out; do
  [ -n "${!required}" ] || fail "--${required//_/-} is required"
done
[ -f "$web/index.html" ] || fail "no $web/index.html: build shell-store first"
mkdir -p "$out/gate"
out="$(cd "$out" && pwd)"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT

# zip_dir <dir> <zip>: index.html at the root of the zip, where the plugin looks first.
zip_dir() {
  rm -f "$2"
  (cd "$1" && zip -q -X -r "$2" .)
}

# sign <id> <zip>: writes <zip minus .zip>.json.
sign() {
  node "$LIVE_UPDATE/sign-bundle.mjs" --zip "$2" --id "$1" \
    --url "$base_url/$(basename "$2")" --minimum-native "$minimum" --out "${2%.zip}.json"
}

release_zip="$out/lodb-bundle-$version.zip"
zip_dir "$web" "$release_zip"
sign "$version" "$release_zip"

mkdir "$scratch/faulty"
cat > "$scratch/faulty/index.html" <<'HTML'
<!doctype html>
<html lang="en">
  <head><meta charset="utf-8"><title>LoDb release gate</title></head>
  <body>This bundle never starts the app.</body>
</html>
HTML
faulty_zip="$out/gate/lodb-bundle-$version-gate-faulty.zip"
zip_dir "$scratch/faulty" "$faulty_zip"
sign "$version-gate-faulty" "$faulty_zip"

tampered_zip="$out/gate/lodb-bundle-$version-gate-tampered.zip"
cp "$release_zip" "$tampered_zip"
echo 'not signed' > "$scratch/tampered.txt"
(cd "$scratch" && zip -q -X "$tampered_zip" tampered.txt)
# The descriptor of the release bundle, moved to the tampered zip: its signature no longer
# holds. The checksum is the tampered zip's, so that only the signature can refuse it.
node - "${release_zip%.zip}.json" "$tampered_zip" "$version-gate-tampered" "$base_url" <<'JS'
const { createHash } = require('node:crypto');
const { readFileSync, writeFileSync } = require('node:fs');
const { basename } = require('node:path');
const [descriptorPath, zip, id, baseUrl] = process.argv.slice(2);
const descriptor = JSON.parse(readFileSync(descriptorPath, 'utf8'));
const checksum = createHash('sha256').update(readFileSync(zip)).digest('hex');
const tampered = { ...descriptor, id, url: `${baseUrl}/${basename(zip)}`, checksum };
writeFileSync(zip.replace(/\.zip$/, '.json'), `${JSON.stringify(tampered, null, 2)}\n`);
JS

echo "bundles.sh: bundle $version (minimum native $minimum) and the gate bundles in $out"
