#!/usr/bin/env bash
# Signs an Android release (ADR 0008, docs/guides/release-android.md); the release workflow
# and verify-local.sh both sign through it:
#
#   sign-release.sh --aab app-release.aab --apk app-release-unsigned.apk --version X.Y.Z \
#     --out DIR
#
#   DIR/lodb-X.Y.Z.aab   signed with the upload key (jarsigner): what Play receives, before
#                        Play App Signing signs the APKs it serves with the app signing key
#   DIR/lodb-X.Y.Z.apk   aligned, then signed with the APK key (apksigner): the app signing key
#                        of the transitional channel, the one Play App Signing imports
#   DIR/signing.txt      the SHA-256 of both certificates
#
# Keys from the environment only (keystore paths; the passwords never reach a command line):
#   LODB_UPLOAD_KEYSTORE, LODB_UPLOAD_KEY_ALIAS, LODB_UPLOAD_STORE_PASSWORD,
#   LODB_UPLOAD_KEY_PASSWORD, and the same four with LODB_APK_.
# Each signature is verified, and its certificate compared with the keystore's.
# Needs keytool and jarsigner (JDK), and the build tools of ANDROID_HOME (or
# ANDROID_BUILD_TOOLS_DIR) for zipalign and apksigner.
set -euo pipefail

fail() {
  echo "sign-release.sh: $*" >&2
  exit 1
}

aab="" apk="" version="" out=""
while [ $# -gt 0 ]; do
  case "$1" in
    --aab) aab="$2"; shift 2 ;;
    --apk) apk="$2"; shift 2 ;;
    --version) version="$2"; shift 2 ;;
    --out) out="$2"; shift 2 ;;
    *) fail "unknown argument '$1'" ;;
  esac
done
for required in aab apk version out; do
  [ -n "${!required}" ] || fail "--$required is required"
done
for key in UPLOAD APK; do
  for part in KEYSTORE KEY_ALIAS STORE_PASSWORD KEY_PASSWORD; do
    variable="LODB_${key}_${part}"
    [ -n "${!variable:-}" ] || fail "$variable is not set"
  done
done
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT
mkdir -p "$out"

# The newest build tools of the SDK, unless ANDROID_BUILD_TOOLS_DIR names some.
build_tools() {
  local dir="${ANDROID_BUILD_TOOLS_DIR:-}"
  if [ -z "$dir" ]; then
    [ -n "${ANDROID_HOME:-}" ] || fail "ANDROID_HOME or ANDROID_BUILD_TOOLS_DIR is required"
    dir="$ANDROID_HOME/build-tools/$(find "$ANDROID_HOME/build-tools" -mindepth 1 -maxdepth 1 \
      -printf '%f\n' | sort -V | tail -n 1)"
  fi
  [ -x "$dir/apksigner" ] || fail "no apksigner in $dir"
  echo "$dir"
}

# sha256_of: the first SHA-256 fingerprint keytool prints, as 64 upper case hex digits.
# Reads its whole input: an early exit would break keytool's pipe under pipefail.
sha256_of() {
  awk '/^[[:space:]]*SHA256:/ && !found { sub(/^[[:space:]]*SHA256: */, ""); print; found = 1 }' \
    | tr -d ':' | tr '[:lower:]' '[:upper:]'
}

# keystore_sha256 UPLOAD|APK: the certificate of the key the variables name.
keystore_sha256() {
  local store="LODB_$1_KEYSTORE" alias="LODB_$1_KEY_ALIAS" listing
  listing="$(keytool -list -v -keystore "${!store}" -alias "${!alias}" \
    -storepass:env "LODB_$1_STORE_PASSWORD")"
  sha256_of <<<"$listing"
}

sign_aab() {
  local signed="$out/lodb-$version.aab" verification printed
  jarsigner -keystore "$LODB_UPLOAD_KEYSTORE" -storepass:env LODB_UPLOAD_STORE_PASSWORD \
    -keypass:env LODB_UPLOAD_KEY_PASSWORD -signedjar "$signed" "$aab" "$LODB_UPLOAD_KEY_ALIAS" \
    > /dev/null
  verification="$(jarsigner -verify "$signed")"
  grep -q '^jar verified\.' <<<"$verification" || fail "jarsigner does not verify $signed"
  printed="$(keytool -printcert -jarfile "$signed" | sha256_of)"
  [ -n "$printed" ] && [ "$printed" = "$(keystore_sha256 UPLOAD)" ] \
    || fail "$signed is not signed by the upload key"
  echo "upload_sha256=$printed" >> "$out/signing.txt"
}

sign_apk() {
  local tools signed="$out/lodb-$version.apk" certificates printed
  tools="$(build_tools)"
  "$tools/zipalign" -f -p 4 "$apk" "$scratch/aligned.apk"
  "$tools/apksigner" sign --ks "$LODB_APK_KEYSTORE" --ks-key-alias "$LODB_APK_KEY_ALIAS" \
    --ks-pass env:LODB_APK_STORE_PASSWORD --key-pass env:LODB_APK_KEY_PASSWORD \
    --out "$signed" "$scratch/aligned.apk"
  "$tools/zipalign" -c -p 4 "$signed" || fail "$signed lost its alignment"
  certificates="$("$tools/apksigner" verify --verbose --print-certs "$signed")"
  grep -q 'Verified using v2 scheme (APK Signature Scheme v2): true' <<<"$certificates" \
    || fail "$signed has no APK Signature Scheme v2 signature"
  printed="$(sed -n 's/^Signer #1 certificate SHA-256 digest: //p' <<<"$certificates" \
    | tr '[:lower:]' '[:upper:]')"
  [ -n "$printed" ] && [ "$printed" = "$(keystore_sha256 APK)" ] \
    || fail "$signed is not signed by the APK key"
  echo "apk_sha256=$printed" >> "$out/signing.txt"
}

rm -f "$out/signing.txt"
sign_aab
sign_apk
echo "sign-release.sh: signed and verified lodb-$version.aab and lodb-$version.apk in $out"
cat "$out/signing.txt"
