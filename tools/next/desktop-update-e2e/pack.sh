#!/usr/bin/env bash
# Publishes LoDb.Desktop for one RID and packs it with vpk into a release folder. Shared by
# the release workflow and local.sh. When the release folder already holds N-1 (vpk
# download), vpk also writes the delta N-1 -> N.
#
# Usage: pack.sh --rid RID --version VERSION --channel stable|beta --shell-dir DIR
#                --releases DIR --publish-dir DIR [-- extra vpk pack arguments]
# The extra arguments carry the signing options, which only the workflow has.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

rid="" version="" channel="" shell_dir="" releases="" publish_dir=""
extra=()
while [ $# -gt 0 ]; do
  case "$1" in
    --rid) rid="$2"; shift 2 ;;
    --version) version="$2"; shift 2 ;;
    --channel) channel="$2"; shift 2 ;;
    --shell-dir) shell_dir="$2"; shift 2 ;;
    --releases) releases="$2"; shift 2 ;;
    --publish-dir) publish_dir="$2"; shift 2 ;;
    --) shift; extra=("$@"); break ;;
    *) lodb_fail "pack.sh: unknown argument '$1'" ;;
  esac
done
for required in rid version channel shell_dir releases publish_dir; do
  [ -n "${!required}" ] || lodb_fail "pack.sh: --${required//_/-} is required"
done
lodb_check_rid "$rid"
[ -f "$shell_dir/index.html" ] || lodb_fail "no shell build in $shell_dir (npm run build:shell)"
# MSBuild resolves a relative LoDbShellDir against the project folder, not the caller's.
shell_dir="$(lodb_native_path "$shell_dir")"

root="$(lodb_repo_root)"
velopack_channel="$(lodb_velopack_channel "$rid" "$channel")"
main_exe="$LODB_PACK_ID"
os_args=()
case "$rid" in
  win-*) main_exe="$LODB_PACK_ID.exe" ;;
  osx-*) os_args=(--bundleId "$LODB_BUNDLE_ID") ;;
esac

lodb_log "publish $LODB_PACK_ID $version ($channel) for $rid"
rm -rf "$publish_dir"
# Version: the tag's version, checked against VersionPrefix by the workflow before this.
dotnet publish "$root/$LODB_DESKTOP_PROJECT" \
  -c Release \
  -r "$rid" \
  --self-contained \
  -o "$publish_dir" \
  -p:Version="$version" \
  -p:LoDbDesktopChannel="$channel" \
  -p:LoDbShellDir="$shell_dir"

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
