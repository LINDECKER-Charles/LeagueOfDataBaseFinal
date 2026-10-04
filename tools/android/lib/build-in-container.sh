#!/usr/bin/env bash
# Runs inside lodb-android-build, started by ../build-debug.sh: builds the debug APK from a
# copy of the repository mounted read-only on /repo, and writes it to /out.
set -euo pipefail

readonly REPO=/repo
readonly WORK=/work
readonly OUT=/out
readonly WEB="$WORK/src/LoDb.Web"
readonly APK="$OUT/lodb-debug.apk"

# The copy leaves out what each side installs or builds for itself: node_modules, the
# builds, Angular's cache, and the Android files that `cap sync` and Gradle produce.
mkdir -p "$WEB"
tar -C "$REPO/src/LoDb.Web" \
  --exclude=./node_modules \
  --exclude=./dist \
  --exclude=./.angular \
  --exclude=./coverage \
  --exclude=./android/.gradle \
  --exclude=./android/build \
  --exclude=./android/app/build \
  --exclude=./android/app/src/main/assets \
  --exclude=./android/capacitor-cordova-android-plugins \
  -cf - . | tar -C "$WEB" -xf -

cd "$WEB"
npm ci --no-audit --no-fund
npm run build:shell:store
npx cap sync android

cd android
./gradlew --no-daemon --console=plain assembleDebug
cp app/build/outputs/apk/debug/app-debug.apk "$APK"

# What was built: identity and SDK levels, the embedded bundle, and its configuration.
build_tools="$(ls "$ANDROID_HOME/build-tools" | sort -V | tail -n 1)"
"$ANDROID_HOME/build-tools/$build_tools/aapt2" dump badging "$APK" \
  | grep -E "^(package|minSdkVersion|targetSdkVersion|application-label):"
unzip -l "$APK" assets/public/index.html
unzip -p "$APK" assets/capacitor.config.json
echo
