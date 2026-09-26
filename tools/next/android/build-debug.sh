#!/usr/bin/env bash
# Builds the debug APK of the Android app in a linux/amd64 container (plan, L10.1).
#
#   tools/next/android/build-debug.sh
#
# 1. Builds the toolchain image lodb-android-build:local (docker/next/android-build/).
# 2. Runs it with the repository mounted read-only on /repo. The container copies what the
#    build reads to its own /work (lib/build-in-container.sh), installs its own
#    node_modules there, builds `shell-store`, syncs the Android project and assembles.
# 3. Writes src/LoDb.Web/dist/android/lodb-debug.apk and prints its package, SDK levels and
#    embedded Capacitor configuration.
#
# The host's node_modules never enters the container, nor does the container write into
# the checkout apart from that APK. npm and Gradle downloads are kept in two volumes
# (lodb-android-npm, lodb-android-gradle); `docker volume rm` them to start cold.
# A heavy task: run it alone, never next to the legacy stack or the E2E (plan, §6).
set -euo pipefail

readonly IMAGE=lodb-android-build:local
readonly PLATFORM=linux/amd64

root="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
out="$root/src/LoDb.Web/dist/android"
mkdir -p "$out"

docker build --platform "$PLATFORM" -t "$IMAGE" "$root/docker/next/android-build"

docker run --rm --platform "$PLATFORM" \
  --memory 6g --cpus 6 \
  -v "$root:/repo:ro" \
  -v "$out:/out" \
  -v lodb-android-npm:/cache/npm \
  -v lodb-android-gradle:/cache/gradle \
  "$IMAGE" bash /repo/tools/next/android/lib/build-in-container.sh

echo "build-debug: $out/lodb-debug.apk"
