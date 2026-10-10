#!/usr/bin/env bash
# Publishes LoDb.Desktop for one RID into a publish folder, which pack.sh then packs. Shared by
# the release workflow, where it runs in a job without any signing secret, and local.sh.
#
# Usage: build.sh --rid RID --version VERSION --channel stable|beta --shell-dir DIR
#                 --publish-dir DIR
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

rid="" version="" channel="" shell_dir="" publish_dir=""
while [ $# -gt 0 ]; do
  case "$1" in
    --rid) rid="$2"; shift 2 ;;
    --version) version="$2"; shift 2 ;;
    --channel) channel="$2"; shift 2 ;;
    --shell-dir) shell_dir="$2"; shift 2 ;;
    --publish-dir) publish_dir="$2"; shift 2 ;;
    *) lodb_fail "build.sh: unknown argument '$1'" ;;
  esac
done
for required in rid version channel shell_dir publish_dir; do
  [ -n "${!required}" ] || lodb_fail "build.sh: --${required//_/-} is required"
done
lodb_check_rid "$rid"
lodb_velopack_channel "$rid" "$channel" >/dev/null
[ -f "$shell_dir/index.html" ] || lodb_fail "no shell build in $shell_dir (npm run build:shell)"
# MSBuild resolves a relative LoDbShellDir against the project folder, not the caller's.
shell_dir="$(lodb_native_path "$shell_dir")"

lodb_log "publish $LODB_PACK_ID $version ($channel) for $rid"
rm -rf "$publish_dir"
# Version: the tag's version, checked against VersionPrefix by the workflow before this.
dotnet publish "$(lodb_repo_root)/$LODB_DESKTOP_PROJECT" \
  -c Release \
  -r "$rid" \
  --self-contained \
  -o "$publish_dir" \
  -p:Version="$version" \
  -p:LoDbDesktopChannel="$channel" \
  -p:LoDbShellDir="$shell_dir"
