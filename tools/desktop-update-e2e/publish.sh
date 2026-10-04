#!/usr/bin/env bash
# Publication step of the release workflow (ADR 0008): uploads the release folder of every
# RID that passed its gate to a GitHub pre-release, then promotes it (stable) or prunes the
# old betas (beta). Nothing is uploaded unless every required RID is there; linux-x64 is
# best effort.
#
# Usage: publish.sh --channel stable|beta --version V --sha SHA --repo OWNER/NAME
#                   --artifacts DIR
#   DIR holds one release folder per RID: DIR/desktop-releases-<rid>.
# Environment: GH_TOKEN (gh and vpk), GITHUB_SERVER_URL.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

readonly REQUIRED_RIDS="win-x64 osx-arm64 osx-x64"
# The app reads the first page of releases (GithubSource): old betas would push the stable
# releases out of it.
readonly BETAS_KEPT=5
readonly RELEASE_LIST_LIMIT=200

channel="" version="" sha="" repo="" artifacts=""
while [ $# -gt 0 ]; do
  case "$1" in
    --channel) channel="$2"; shift 2 ;;
    --version) version="$2"; shift 2 ;;
    --sha) sha="$2"; shift 2 ;;
    --repo) repo="$2"; shift 2 ;;
    --artifacts) artifacts="$2"; shift 2 ;;
    *) lodb_fail "publish.sh: unknown argument '$1'" ;;
  esac
done
for required in channel version sha repo artifacts; do
  [ -n "${!required}" ] || lodb_fail "publish.sh: --$required is required"
done
lodb_velopack_channel win-x64 "$channel" >/dev/null
tag="$(lodb_release_tag "$version")"

folder_of() { printf '%s/desktop-releases-%s\n' "$artifacts" "$1"; }

check_required() {
  local rid
  for rid in $REQUIRED_RIDS; do
    [ -d "$(folder_of "$rid")" ] || lodb_fail "$rid has no release folder: nothing is published"
  done
}

upload() {
  local rid
  # Through the environment: a --token argument would show in the process list.
  export VPK_TOKEN="${GH_TOKEN:-}"
  for rid in $LODB_RIDS; do
    if [ ! -d "$(folder_of "$rid")" ]; then
      lodb_log "$rid is not part of $version (best effort)"
      continue
    fi
    lodb_log "upload of $rid to the pre-release $tag"
    lodb_vpk upload github --repoUrl "${GITHUB_SERVER_URL:-https://github.com}/$repo" \
      --channel "$(lodb_velopack_channel "$rid" "$channel")" --outputDir "$(folder_of "$rid")" \
      --tag "$tag" --releaseName "$LODB_PACK_TITLE desktop $version" \
      --targetCommitish "$sha" --pre --merge --publish
  done
}

# Until now /releases/latest still pointed at N-1: never a window with a partial release.
promote() {
  local assets rid
  assets="$(gh release view "$tag" --repo "$repo" --json assets --jq '.assets[].name')"
  for rid in $REQUIRED_RIDS; do
    grep -qxF "releases.$rid.json" <<<"$assets" \
      || lodb_fail "$tag has no feed for $rid: it stays a pre-release"
  done
  gh release edit "$tag" --repo "$repo" --prerelease=false --latest
  lodb_log "$tag promoted"
}

prune_betas() {
  local old
  gh release list --repo "$repo" --limit "$RELEASE_LIST_LIMIT" \
    --json tagName,isPrerelease,createdAt \
    --jq '[.[] | select(.isPrerelease and (.tagName | test("^desktop-v.+-beta[.][0-9]+$")))]
      | sort_by(.createdAt) | reverse | .[].tagName' \
    | tail -n "+$((BETAS_KEPT + 1))" \
    | while IFS= read -r old; do
        lodb_log "deleting the old beta $old"
        gh release delete "$old" --repo "$repo" --cleanup-tag --yes
      done
}

check_required
upload
case "$channel" in
  stable) promote ;;
  beta) prune_betas ;;
esac
lodb_log "desktop $version published ($channel)"
