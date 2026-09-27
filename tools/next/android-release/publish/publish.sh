#!/usr/bin/env bash
# Publishes a staged Android release once its gate passed (ADR 0008):
#
#   publish.sh --version X.Y.Z --repo owner/name --release DIR --transitional true|false
#
# With the transitional channel on: uploads the signed APK and lodb-android-latest.json, then
# downloads the APK back from the URL of the manifest and checks its SHA-256. Then promotes
# the pre-release, never as the repository's latest release: that one is the desktop's feed.
# Needs gh (GH_TOKEN with contents: write) and curl.
set -euo pipefail

MANIFEST_TOOL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/latest-manifest.mjs"
readonly MANIFEST_TOOL
readonly TAG_PREFIX=android-v
readonly MANIFEST_NAME=lodb-android-latest.json

fail() {
  echo "publish.sh: $*" >&2
  exit 1
}

version="" repo="" release="" transitional=""
while [ $# -gt 0 ]; do
  case "$1" in
    --version) version="$2"; shift 2 ;;
    --repo) repo="$2"; shift 2 ;;
    --release) release="$2"; shift 2 ;;
    --transitional) transitional="$2"; shift 2 ;;
    *) fail "unknown argument '$1'" ;;
  esac
done
for required in version repo release transitional; do
  [ -n "${!required}" ] || fail "--$required is required"
done
tag="$TAG_PREFIX$version"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT

publish_apk() {
  local apk="$release/lodb-$version.apk"
  local url="https://github.com/$repo/releases/download/$tag/lodb-$version.apk"
  node "$MANIFEST_TOOL" --apk "$apk" --version "$version" --url "$url" \
    --out "$scratch/$MANIFEST_NAME"
  gh release upload "$tag" --repo "$repo" --clobber "$apk" "$scratch/$MANIFEST_NAME"
  curl -fsSL --retry 5 --retry-all-errors -o "$scratch/downloaded.apk" "$url"
  node "$MANIFEST_TOOL" --verify "$scratch/$MANIFEST_NAME" --apk "$scratch/downloaded.apk"
}

case "$transitional" in
  true) publish_apk ;;
  false) echo "publish.sh: transitional channel off, no APK published" ;;
  *) fail "--transitional must be true or false, got '$transitional'" ;;
esac

gh release edit "$tag" --repo "$repo" --prerelease=false --latest=false
echo "publish.sh: $tag published"
