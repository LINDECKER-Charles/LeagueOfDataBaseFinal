#!/usr/bin/env bash
# Stages an Android release on GitHub before its gate (ADR 0008):
#
#   stage.sh --version X.Y.Z --repo owner/name --bundles DIR
#
# Creates the release android-vX.Y.Z on its tag as a pre-release, uploads the bundle of the
# release with its descriptor, and the gate bundles: the emulator downloads them from their
# public URLs, as a device would. Then downloads the release bundle back from the URL its
# descriptor names, and verifies it against LODB_LIVE_UPDATE_PUBLIC_KEY.
# Needs gh (GH_TOKEN with contents: write), curl and jq. Never touches a promoted release.
set -euo pipefail

LIVE_UPDATE="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../live-update" && pwd)"
readonly LIVE_UPDATE
readonly TAG_PREFIX=android-v

fail() {
  echo "stage.sh: $*" >&2
  exit 1
}

version="" repo="" bundles=""
while [ $# -gt 0 ]; do
  case "$1" in
    --version) version="$2"; shift 2 ;;
    --repo) repo="$2"; shift 2 ;;
    --bundles) bundles="$2"; shift 2 ;;
    *) fail "unknown argument '$1'" ;;
  esac
done
for required in version repo bundles; do
  [ -n "${!required}" ] || fail "--$required is required"
done
tag="$TAG_PREFIX$version"
descriptor="$bundles/lodb-bundle-$version.json"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT

# A re-run after a failed gate finds its own pre-release.
state="$(gh release view "$tag" --repo "$repo" --json isPrerelease --jq .isPrerelease \
  2> /dev/null || true)"
case "$state" in
  false) fail "$tag is already published" ;;
  true) echo "stage.sh: $tag is staged already, its assets are replaced" ;;
  *)
    gh release create "$tag" --repo "$repo" --verify-tag --prerelease --latest=false \
      --title "Android $version" \
      --notes "Android $version: pre-release until the update gate passes." ;;
esac

gh release upload "$tag" --repo "$repo" --clobber \
  "$bundles/lodb-bundle-$version.zip" "$descriptor" "$bundles"/gate/*.zip

# What the policy will point at is what was signed.
curl -fsSL --retry 5 --retry-all-errors -o "$scratch/bundle.zip" "$(jq -r .url "$descriptor")"
node "$LIVE_UPDATE/verify-bundle.mjs" --zip "$scratch/bundle.zip" --descriptor "$descriptor"
echo "stage.sh: $tag staged"
