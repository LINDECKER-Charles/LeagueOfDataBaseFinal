#!/usr/bin/env bash
# Fetches N-1 of a channel from GitHub Releases, before vpk pack (ADR 0008): its full
# package into the release folder, from which vpk then writes the delta N-1 -> N, and its
# installable build (lodb_installable) for gate.sh. vpk picks N-1 as the app does: the
# newest release of the channel, pre-releases on the beta channel only.
#
# Usage: previous.sh --rid RID --channel stable|beta --repo OWNER/NAME --releases DIR
#                    --installable-dir DIR
# Environment: GH_TOKEN (gh, and vpk against the API rate limit), GITHUB_SERVER_URL.
# Prints N-1's version, or nothing for the first release of the channel.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

rid="" channel="" repo="" releases="" installable_dir=""
while [ $# -gt 0 ]; do
  case "$1" in
    --rid) rid="$2"; shift 2 ;;
    --channel) channel="$2"; shift 2 ;;
    --repo) repo="$2"; shift 2 ;;
    --releases) releases="$2"; shift 2 ;;
    --installable-dir) installable_dir="$2"; shift 2 ;;
    *) lodb_fail "previous.sh: unknown argument '$1'" ;;
  esac
done
for required in rid channel repo releases installable_dir; do
  [ -n "${!required}" ] || lodb_fail "previous.sh: --${required//_/-} is required"
done
lodb_check_rid "$rid"
velopack_channel="$(lodb_velopack_channel "$rid" "$channel")"
# A leftover package would pass for N-1.
[ ! -e "$releases" ] || [ -z "$(ls -A "$releases")" ] \
  || lodb_fail "previous.sh: $releases must be empty"
mkdir -p "$releases" "$installable_dir"

download_args=(--repoUrl "${GITHUB_SERVER_URL:-https://github.com}/$repo")
download_args+=(--channel "$velopack_channel" --outputDir "$releases")
[ "$channel" = stable ] || download_args+=(--pre)
# Through the environment: a --token argument would show in the process list.
[ -z "${GH_TOKEN:-}" ] || export VPK_TOKEN="$GH_TOKEN"
lodb_log "vpk download of the newest release of $velopack_channel"
# stdout carries the version only; vpk exits 0 without downloading when there is no release.
lodb_vpk download github "${download_args[@]}" >&2

full_packages=()
while IFS= read -r package; do
  full_packages+=("$package")
done < <(find "$releases" -maxdepth 1 -name "$LODB_PACK_ID-*-$velopack_channel-full.nupkg")
case "${#full_packages[@]}" in
  0) lodb_log "no release of $velopack_channel yet: no delta, no update to gate"
    exit 0 ;;
  1) ;;
  *) lodb_fail "several full packages downloaded: ${full_packages[*]}" ;;
esac
previous="$(basename "${full_packages[0]}")"
previous="${previous#"$LODB_PACK_ID-"}"
previous="${previous%"-$velopack_channel-full.nupkg"}"

installable="$(lodb_installable "$rid" "$velopack_channel")"
lodb_log "N-1 is $previous: gh release download of $installable"
gh release download "$(lodb_release_tag "$previous")" --repo "$repo" \
  --pattern "$installable" --dir "$installable_dir" --clobber
[ -f "$installable_dir/$installable" ] || lodb_fail "$installable is missing from N-1"
printf '%s\n' "$previous"
