#!/usr/bin/env bash
# Signing setup of the release workflow (ADR 0008), from the secrets and variables of the
# desktop-release environment (docs/guides/release-desktop.md). Writes the vpk pack options
# that sign, one per line, into DIR/sign-args. A missing value fails: an unsigned build is
# never published.
#
# Usage: signing.sh macos|windows|cleanup DIR
#   macos    Developer ID certificates in a keychain of this run (DIR), notarytool profile
#            in it. Environment: APPLE_DEVELOPER_ID_P12 (base64), its _PASSWORD,
#            APPLE_NOTARY_KEY_P8 (base64), APPLE_NOTARY_KEY_ID, APPLE_NOTARY_ISSUER_ID,
#            APPLE_APP_IDENTITY, APPLE_INSTALL_IDENTITY.
#   windows  Azure Artifact Signing metadata; the Azure CLI session (OIDC) signs.
#            Environment: AZURE_SIGNING_ENDPOINT, AZURE_SIGNING_ACCOUNT,
#            AZURE_SIGNING_PROFILE.
#   cleanup  removes the keychain of the macos setup, if any.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

readonly NOTARY_PROFILE=lodb-notary
# Seconds the keychain stays unlocked: longer than the pack job's timeout.
readonly KEYCHAIN_UNLOCKED_SECONDS=21600

[ $# -eq 2 ] || lodb_fail "usage: signing.sh macos|windows|cleanup DIR"
command="$1" dir="$2"
case "$command" in
  macos | windows | cleanup) ;;
  *) lodb_fail "unknown command '$command' (macos, windows or cleanup)" ;;
esac
mkdir -p "$dir"
options="$dir/sign-args"
keychain="$dir/lodb-signing.keychain-db"

require() {
  local missing="" name
  for name in "$@"; do
    [ -n "${!name:-}" ] || missing="$missing $name"
  done
  [ -z "$missing" ] || lodb_fail "an unsigned build is never published; missing:$missing"
}

# decode NAME FILE: the base64 value of variable NAME into FILE.
decode() {
  printf '%s' "${!1}" | base64 --decode >"$2"
}

create_keychain() {
  local password
  password="$(openssl rand -base64 32)"
  [ "${GITHUB_ACTIONS:-}" != true ] || echo "::add-mask::$password"
  security create-keychain -p "$password" "$keychain"
  security set-keychain-settings -lut "$KEYCHAIN_UNLOCKED_SECONDS" "$keychain"
  security unlock-keychain -p "$password" "$keychain"
  decode APPLE_DEVELOPER_ID_P12 "$dir/developer-id.p12"
  security import "$dir/developer-id.p12" -k "$keychain" -f pkcs12 \
    -P "$APPLE_DEVELOPER_ID_P12_PASSWORD" -T /usr/bin/codesign -T /usr/bin/productsign
  rm -f "$dir/developer-id.p12"
  security set-key-partition-list -S apple-tool:,apple:,codesign: -s \
    -k "$password" "$keychain" >/dev/null
}

# codesign and productsign look identities up in the user's search list.
search_keychain() {
  local searched=() line
  while IFS= read -r line; do
    searched+=("$(sed -e 's/^ *"//' -e 's/"$//' <<<"$line")")
  done < <(security list-keychains -d user)
  security list-keychains -d user -s "$keychain" ${searched[@]+"${searched[@]}"}
}

setup_macos() {
  require APPLE_DEVELOPER_ID_P12 APPLE_DEVELOPER_ID_P12_PASSWORD APPLE_NOTARY_KEY_P8 \
    APPLE_NOTARY_KEY_ID APPLE_NOTARY_ISSUER_ID APPLE_APP_IDENTITY APPLE_INSTALL_IDENTITY
  create_keychain
  search_keychain
  decode APPLE_NOTARY_KEY_P8 "$dir/notary-key.p8"
  xcrun notarytool store-credentials "$NOTARY_PROFILE" --key "$dir/notary-key.p8" \
    --key-id "$APPLE_NOTARY_KEY_ID" --issuer "$APPLE_NOTARY_ISSUER_ID" --keychain "$keychain"
  rm -f "$dir/notary-key.p8"
  printf '%s\n' --signAppIdentity "$APPLE_APP_IDENTITY" \
    --signInstallIdentity "$APPLE_INSTALL_IDENTITY" \
    --notaryProfile "$NOTARY_PROFILE" --keychain "$keychain" >"$options"
}

setup_windows() {
  require AZURE_SIGNING_ENDPOINT AZURE_SIGNING_ACCOUNT AZURE_SIGNING_PROFILE
  local metadata="$dir/artifact-signing.json"
  jq -n --arg endpoint "$AZURE_SIGNING_ENDPOINT" --arg account "$AZURE_SIGNING_ACCOUNT" \
    --arg profile "$AZURE_SIGNING_PROFILE" \
    '{Endpoint: $endpoint, CodeSigningAccountName: $account,
      CertificateProfileName: $profile}' >"$metadata"
  printf '%s\n' --azureTrustedSignFile "$(lodb_native_path "$dir")/artifact-signing.json" \
    >"$options"
}

case "$command" in
  macos) setup_macos ;;
  windows) setup_windows ;;
  cleanup) [ ! -f "$keychain" ] || security delete-keychain "$keychain" ;;
esac
lodb_log "signing: $command done"
