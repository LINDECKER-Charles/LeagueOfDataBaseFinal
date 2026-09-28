#!/usr/bin/env bash
# The update gate (ADR 0008): an installed N-1 updates itself to N from a local feed, and
# --smoke shows N-1 before and N after. The release workflow runs it on each RID before
# publishing; local.sh runs it on macOS arm64, unsigned.
#
# Usage: gate.sh --rid RID --channel stable|beta --to N --work DIR
#                [--from N-1 --feed DIR] [--artifact FILE] [--expect-delta]
#   --from, --feed  the update to check; without them (first release of a channel, no
#                   N-1), the gate only checks that the installed N starts: --smoke shows N.
#   --artifact      installs first, from a portable zip (win, osx) or an AppImage (linux);
#                   without it, the install already under WORK/install is used.
#   --expect-delta  N must be applied from deltas: the updater's cache holds N-1 (a second
#                   hop, after a first update).
# Reports: WORK/logs/{before,apply,after}-<versions>.out (smoke reports) and .err (JSON
# logs and updater output).
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

readonly UPDATER_TIMEOUT_SECONDS=180
readonly SMOKE_ATTEMPTS=3

rid="" channel="" from="" to="" feed="" work="" artifact="" expect_delta=false
while [ $# -gt 0 ]; do
  case "$1" in
    --rid) rid="$2"; shift 2 ;;
    --channel) channel="$2"; shift 2 ;;
    --from) from="$2"; shift 2 ;;
    --to) to="$2"; shift 2 ;;
    --feed) feed="$2"; shift 2 ;;
    --work) work="$2"; shift 2 ;;
    --artifact) artifact="$2"; shift 2 ;;
    --expect-delta) expect_delta=true; shift ;;
    *) lodb_fail "gate.sh: unknown argument '$1'" ;;
  esac
done
for required in rid channel to work; do
  [ -n "${!required}" ] || lodb_fail "gate.sh: --$required is required"
done
[ "${from:+set}" = "${feed:+set}" ] || lodb_fail "gate.sh: --from and --feed go together"
lodb_check_rid "$rid"

mkdir -p "$work/install" "$work/data" "$work/logs"
# Physical: the updater runs from the resolved path (/private/tmp, not /tmp, on macOS).
install_dir="$(cd "$work/install" && pwd -P)"
data_dir="$(lodb_native_path "$work/data")"
logs="$work/logs"

install_from() {
  lodb_log "install $(basename "$1")"
  rm -rf "$work/install" && mkdir -p "$work/install"
  case "$rid" in
    osx-*) ditto -x -k "$1" "$work/install" ;;
    # The bsdtar of Windows reads zip archives; Git Bash has no unzip, and its tar no zip.
    win-*) "$SYSTEMROOT/System32/tar.exe" -xf \
      "$(lodb_native_path "$(dirname "$1")")/$(basename "$1")" \
      -C "$(lodb_native_path "$work/install")" ;;
    linux-*) cp "$1" "$work/install/$LODB_PACK_ID.AppImage"
      chmod +x "$work/install/$LODB_PACK_ID.AppImage" ;;
  esac
}

app_executable() {
  case "$rid" in
    osx-*)
      local bundle
      bundle="$(find "$work/install" -maxdepth 1 -name '*.app' | head -n 1)"
      [ -n "$bundle" ] || lodb_fail "no .app bundle under $work/install"
      printf '%s/Contents/MacOS/%s\n' "$bundle" "$LODB_PACK_ID" ;;
    win-*) printf '%s/current/%s.exe\n' "$work/install" "$LODB_PACK_ID" ;;
    linux-*) printf '%s/%s.AppImage\n' "$work/install" "$LODB_PACK_ID" ;;
  esac
}

# The updater of this install, still running (it waits for the app, then replaces it).
is_updater_running() {
  case "$rid" in
    osx-*) pgrep -fl UpdateMac | grep -qF -- "$install_dir" ;;
    linux-*) pgrep -f UpdateNix >/dev/null ;;
    win-*) tasklist //FI "IMAGENAME eq Update.exe" //NH | grep -qi 'Update.exe' ;;
  esac
}

wait_for_updater() {
  local waited=0
  sleep 1
  while is_updater_running; do
    [ "$waited" -lt "$UPDATER_TIMEOUT_SECONDS" ] \
      || lodb_fail "the updater still runs after ${waited}s"
    sleep 1
    waited=$((waited + 1))
  done
  lodb_log "updater done after ~$((waited + 1))s"
}

# smoke NAME [ARGS...]: runs --smoke with the given arguments; report in $logs/NAME.out.
smoke() {
  local name="$1"
  shift
  local status=0
  LODB_DESKTOP_DATA_DIR="$data_dir" "$(app_executable)" --smoke "$@" \
    >"$logs/$name.out" 2>"$logs/$name.err" || status=$?
  sed 's/^/    /' "$logs/$name.out" >&2
  return "$status"
}

expect_report() {
  local name="$1" version="$2" updates="$3"
  local header="LoDb.Desktop $version (channel $channel)"
  [ "$(head -n 1 "$logs/$name.out")" = "$header" ] || lodb_fail "$name: expected '$header'"
  grep -qxF -- "updates: $updates" "$logs/$name.out" \
    || lodb_fail "$name: expected 'updates: $updates'"
  grep -qxF -- "smoke: ok" "$logs/$name.out" || lodb_fail "$name: the smoke check failed"
}

delta_count() {
  grep -o '"DeltaCount":[0-9]*' "$logs/$1.err" | head -n 1 | cut -d: -f2
}

[ -z "$artifact" ] || install_from "$artifact"
if [ -z "$from" ]; then
  lodb_log "no N-1: smoke of the installed version ($to) only"
  smoke "start-$to" || lodb_fail "start-$to: --smoke exited with $?"
  expect_report "start-$to" "$to" none
  lodb_log "gate passed: $to starts on $rid ($channel), no update to check"
  exit 0
fi

feed_dir="$(lodb_native_path "$feed")"
before="before-$from" apply="apply-$from-$to" after="after-$to"

lodb_log "smoke of the installed version ($from)"
smoke "$before" || lodb_fail "$before: --smoke exited with $?"
expect_report "$before" "$from" none

lodb_log "update $from -> $to from $feed_dir"
LODB_DESKTOP_UPDATE_SOURCE="$feed_dir" smoke "$apply" --apply-updates \
  || lodb_fail "$apply: --smoke --apply-updates exited with $?"
expect_report "$apply" "$from" "ready $to"
deltas="$(delta_count "$apply")"
lodb_log "downloaded $to with ${deltas:-?} delta(s)"
if [ "$expect_delta" = true ] && [ "${deltas:-0}" -lt 1 ]; then
  lodb_fail "$apply: $to was downloaded in full, deltas were expected"
fi
wait_for_updater

lodb_log "smoke of the updated version ($to)"
for attempt in $(seq 1 "$SMOKE_ATTEMPTS"); do
  smoke "$after" || true
  if [ "$(head -n 1 "$logs/$after.out")" = "LoDb.Desktop $to (channel $channel)" ]; then
    break
  fi
  [ "$attempt" -lt "$SMOKE_ATTEMPTS" ] || break
  sleep 2
done
expect_report "$after" "$to" none
lodb_log "gate passed: $from -> $to on $rid ($channel), ${deltas:-0} delta(s)"
