#!/usr/bin/env bash
# Runs inside lodb-android-build, started by ../verify-local.sh: builds the release AAB and
# APK from a copy of the repository mounted read-only on /repo, signs them with throwaway
# keystores through ../sign-release.sh, and writes them to /out.
#
#   release-in-container.sh X.Y.Z
set -euo pipefail

readonly VERSION="$1"
readonly REPO=/repo
readonly WORK=/work
readonly OUT=/out
readonly WEB="$WORK/src/LoDb.Web"
readonly RELEASE="$REPO/tools/next/android-release"
# Throwaway keys: 4096 bits as the guide asks of the real ones, valid one day.
readonly KEY_BITS=4096
readonly KEY_DAYS=1

keys="$(mktemp -d)"
trap 'rm -rf "$keys"' EXIT

version_code="$(node --input-type=module -e "
  import { versionCodeOf } from '$RELEASE/lib/release-version.mjs';
  console.log(versionCodeOf(process.argv[1]));
" "$VERSION")"

# The copy leaves out what each side installs or builds for itself (the excludes of
# ../../android/lib/build-in-container.sh).
mkdir -p "$WEB" "$WORK/app/public"
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
# Target of the committed symlink src/app/features/editorial/changelog/published (L3.10).
cp -R "$REPO/app/public/changelog" "$WORK/app/public/changelog"

# A throwaway bundle key too, for a capacitor.config.ts that embeds the public one.
LODB_LIVE_UPDATE_PUBLIC_KEY="$(node -e "
  const { generateKeyPairSync } = require('node:crypto');
  const { publicKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
  process.stdout.write(publicKey.export({ type: 'spki', format: 'pem' }));
")"
export LODB_LIVE_UPDATE_PUBLIC_KEY

cd "$WEB"
npm ci --no-audit --no-fund
npm run build:shell:store
npx cap sync android
cd android
./gradlew --no-daemon --console=plain bundleRelease assembleRelease \
  -PlodbVersionCode="$version_code" -PlodbVersionName="$VERSION"

# throwaway_keystore UPLOAD|APK <alias>: a PKCS12 keystore (one password for the store and
# the key, as PKCS12 wants), described by the variables sign-release.sh reads.
throwaway_keystore() {
  local prefix="LODB_$1" alias="$2" password
  password="$(head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n')"
  export "${prefix}_KEYSTORE=$keys/$alias.p12" "${prefix}_KEY_ALIAS=$alias"
  export "${prefix}_STORE_PASSWORD=$password" "${prefix}_KEY_PASSWORD=$password"
  keytool -genkeypair -storetype PKCS12 -keystore "$keys/$alias.p12" -alias "$alias" \
    -keyalg RSA -keysize "$KEY_BITS" -validity "$KEY_DAYS" \
    -dname "CN=LoDb throwaway $alias, O=local release verification" \
    -storepass:env "${prefix}_STORE_PASSWORD" -keypass:env "${prefix}_KEY_PASSWORD" 2> /dev/null
}

throwaway_keystore UPLOAD upload
throwaway_keystore APK transitional

bash "$RELEASE/sign-release.sh" \
  --aab app/build/outputs/bundle/release/app-release.aab \
  --apk app/build/outputs/apk/release/app-release-unsigned.apk \
  --version "$VERSION" --out "$OUT"

# What was signed: the certificates, and the identity and version of the APK.
build_tools="$ANDROID_HOME/build-tools/$(find "$ANDROID_HOME/build-tools" -mindepth 1 \
  -maxdepth 1 -printf '%f\n' | sort -V | tail -n 1)"
keytool -printcert -jarfile "$OUT/lodb-$VERSION.aab" | grep -E '^(Owner|[[:space:]]*SHA256):'
"$build_tools/apksigner" verify --print-certs "$OUT/lodb-$VERSION.apk" | grep -E 'DN|SHA-256'
"$build_tools/aapt2" dump badging "$OUT/lodb-$VERSION.apk" | grep -E '^package:'
