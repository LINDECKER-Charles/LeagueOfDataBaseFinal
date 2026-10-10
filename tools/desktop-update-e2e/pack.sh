#!/usr/bin/env bash
# Packs the publish folder of build.sh with vpk into a release folder. Shared by the release
# workflow and local.sh. When the release folder already holds N-1 (vpk download), vpk also
# writes the delta N-1 -> N. Builds nothing: in the workflow, it runs in the signing job,
# which never runs the .NET build.
#
# Usage: pack.sh --rid RID --version VERSION --channel stable|beta --publish-dir DIR
#                --releases DIR [-- extra vpk pack arguments]
# The extra arguments carry the signing options, which only the workflow has.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

rid="" version="" channel="" releases="" publish_dir=""
extra=()
while [ $# -gt 0 ]; do
  case "$1" in
    --rid) rid="$2"; shift 2 ;;
    --version) version="$2"; shift 2 ;;
    --channel) channel="$2"; shift 2 ;;
    --releases) releases="$2"; shift 2 ;;
    --publish-dir) publish_dir="$2"; shift 2 ;;
    --) shift; extra=("$@"); break ;;
    *) lodb_fail "pack.sh: unknown argument '$1'" ;;
  esac
done
for required in rid version channel releases publish_dir; do
  [ -n "${!required}" ] || lodb_fail "pack.sh: --${required//_/-} is required"
done
lodb_check_rid "$rid"

velopack_channel="$(lodb_velopack_channel "$rid" "$channel")"
main_exe="$LODB_PACK_ID"
os_args=()
case "$rid" in
  win-*) main_exe="$LODB_PACK_ID.exe" ;;
  osx-*) os_args=(--bundleId "$LODB_BUNDLE_ID") ;;
esac
[ -f "$publish_dir/$main_exe" ] || lodb_fail "no $main_exe in $publish_dir: run build.sh first"

lodb_log "vpk pack $version into $releases (channel $velopack_channel)"
mkdir -p "$releases"
lodb_vpk pack \
  --packId "$LODB_PACK_ID" \
  --packVersion "$version" \
  --packDir "$publish_dir" \
  --packTitle "$LODB_PACK_TITLE" \
  --packAuthors "$LODB_PACK_AUTHORS" \
  --mainExe "$main_exe" \
  --runtime "$rid" \
  --channel "$velopack_channel" \
  --outputDir "$releases" \
  ${os_args[@]+"${os_args[@]}"} \
  ${extra[@]+"${extra[@]}"}
