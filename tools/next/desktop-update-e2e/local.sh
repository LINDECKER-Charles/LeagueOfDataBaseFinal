#!/usr/bin/env bash
# Local update E2E, unsigned, on macOS arm64: the exit criterion of lot 9. Packs N-1 and N
# from this checkout, publishes N-1 to a local feed, fetches it back as the workflow does
# (vpk download, so that vpk writes the delta), installs N-1, then runs gate.sh: --smoke
# shows N-1, the update to N is applied, --smoke shows N. A second hop N -> N+1 must then
# be applied from the delta, the updater's cache now holding N.
#
# Usage: local.sh [--work DIR] [--keep] [--no-delta-hop]
# Environment: LODB_SHELL_DIR (default src/LoDb.Web/dist/shell/browser, npm run
# build:shell), FROM_VERSION (1.0.0), TO_VERSION (1.0.1), NEXT_VERSION (1.0.2).
# Needs dotnet and vpk; no network beyond NuGet, no Docker, no signing.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$here/lib.sh"

readonly RID=osx-arm64
readonly CHANNEL=stable
from="${FROM_VERSION:-1.0.0}"
to="${TO_VERSION:-1.0.1}"
next="${NEXT_VERSION:-1.0.2}"
work="" keep=false delta_hop=true
while [ $# -gt 0 ]; do
  case "$1" in
    --work) work="$2"; shift 2 ;;
    --keep) keep=true; shift ;;
    --no-delta-hop) delta_hop=false; shift ;;
    *) lodb_fail "local.sh: unknown argument '$1'" ;;
  esac
done

[ "$(uname -s)/$(uname -m)" = "Darwin/arm64" ] || lodb_fail "local.sh runs on macOS arm64"
root="$(lodb_repo_root)"
shell_dir="${LODB_SHELL_DIR:-$root/src/LoDb.Web/dist/shell/browser}"
[ -f "$shell_dir/index.html" ] \
  || lodb_fail "no shell build in $shell_dir: npm ci --prefix src/LoDb.Web, then build:shell"
[ -n "$work" ] || work="$(mktemp -d -t lodb-desktop-e2e)"
mkdir -p "$work"
work="$(cd "$work" && pwd -P)"
velopack_channel="$(lodb_velopack_channel "$RID" "$CHANNEL")"

# The updater's cache is per package id, shared with a real install of the app: removed at
# the end only when this run created it.
cache="$HOME/Library/Caches/velopack/$LODB_PACK_ID"
updater_log="$HOME/Library/Logs/velopack_$LODB_PACK_ID.log"
had_cache=false had_log=false
[ ! -e "$cache" ] || had_cache=true
[ ! -e "$updater_log" ] || had_log=true
cleanup() {
  [ "$had_cache" = true ] || rm -rf "$cache"
  [ "$had_log" = true ] || rm -f "$updater_log"
  [ "$keep" = true ] || rm -rf "$work"
}
trap cleanup EXIT

pack() {
  "$here/pack.sh" --rid "$RID" --version "$1" --channel "$CHANNEL" --shell-dir "$shell_dir" \
    --releases "$2" --publish-dir "$work/publish-$1" >"$work/pack-$1.log" 2>&1 \
    || { tail -n 30 "$work/pack-$1.log" >&2; lodb_fail "pack of $1 failed"; }
}

expect_delta_package() {
  [ -f "$work/releases/$LODB_PACK_ID-$1-$velopack_channel-delta.nupkg" ] \
    || lodb_fail "vpk wrote no delta for $1"
  lodb_log "delta package of $1 written"
}

lodb_log "work folder: $work"
lodb_log "pack N-1 ($from), published to the local feed"
pack "$from" "$work/published"
lodb_log "vpk download of N-1, as the workflow does from GitHub Releases"
lodb_vpk download local --path "$work/published" --channel "$velopack_channel" \
  --outputDir "$work/releases" >"$work/download.log" 2>&1 \
  || { cat "$work/download.log" >&2; lodb_fail "vpk download failed"; }
lodb_log "pack N ($to) over it"
pack "$to" "$work/releases"
expect_delta_package "$to"

"$here/gate.sh" --rid "$RID" --channel "$CHANNEL" --from "$from" --to "$to" \
  --feed "$work/releases" --work "$work/gate" \
  --artifact "$work/published/$(lodb_installable "$RID" "$velopack_channel")"

if [ "$delta_hop" = true ]; then
  lodb_log "pack N+1 ($next) for the delta hop"
  pack "$next" "$work/releases"
  expect_delta_package "$next"
  "$here/gate.sh" --rid "$RID" --channel "$CHANNEL" --from "$to" --to "$next" \
    --feed "$work/releases" --work "$work/gate" --expect-delta
fi

lodb_log "update E2E passed ($from -> $to$([ "$delta_hop" = false ] || echo " -> $next"))"
[ "$keep" = false ] || lodb_log "reports kept in $work/gate/logs"
