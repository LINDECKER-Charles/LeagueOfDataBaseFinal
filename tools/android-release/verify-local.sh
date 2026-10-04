#!/usr/bin/env bash
# Verifies the signature of an Android release locally (L10.4), with throwaway keystores, in
# the container of the Android build (docker/android-build, as ../android/build-debug.sh):
#
#   tools/android-release/verify-local.sh [--version X.Y.Z]
#
# 1. Builds the toolchain image lodb-android-build:local.
# 2. In the container, from a copy of the checkout mounted read-only: npm ci,
#    build:shell:store, cap sync android, then gradlew bundleRelease assembleRelease with the
#    versionCode of X.Y.Z (lib/release-in-container.sh).
# 3. Generates two throwaway keystores inside the container (upload key, APK key), and signs
#    with sign-release.sh, the script of the release workflow: jarsigner for the AAB,
#    zipalign and apksigner for the APK, each certificate checked against its keystore.
# 4. Writes src/LoDb.Web/dist/android-release/: lodb-X.Y.Z.aab, lodb-X.Y.Z.apk, signing.txt.
#
# No real key is read: the keystores die with the container, and their certificates say
# "LoDb throwaway". Never publish what it builds. A heavy task (about 6 GiB): run it alone,
# never next to the legacy stack, another Android build or the E2E (plan, §6).
set -euo pipefail

readonly IMAGE=lodb-android-build:local
readonly PLATFORM=linux/amd64
# A version any release scheme accepts; the build is thrown away anyway.
readonly DEFAULT_VERSION=1.0.0

version="$DEFAULT_VERSION"
while [ $# -gt 0 ]; do
  case "$1" in
    --version) version="$2"; shift 2 ;;
    *) echo "verify-local.sh: unknown argument '$1'" >&2; exit 1 ;;
  esac
done

root="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
out="$root/src/LoDb.Web/dist/android-release"
mkdir -p "$out"

docker build --platform "$PLATFORM" -t "$IMAGE" "$root/docker/android-build"

docker run --rm --platform "$PLATFORM" \
  --memory 6g --cpus 6 \
  -v "$root:/repo:ro" \
  -v "$out:/out" \
  -v lodb-android-npm:/cache/npm \
  -v lodb-android-gradle:/cache/gradle \
  "$IMAGE" bash /repo/tools/android-release/lib/release-in-container.sh "$version"

echo "verify-local.sh: release signature verified, see $out/signing.txt"
