#!/usr/bin/env bash
# The update gate (ADR 0008): an installed N-1 updates itself to N from a local feed, and
# --smoke shows N-1 before and N after. N must then trust the key the feed is signed with, and
# ignore that feed once its signature no longer holds. The release workflow runs it on each
# RID before publishing; local.sh runs it on macOS arm64, with a throwaway feed key and no
# platform signature.
#
# Usage: gate.sh --rid RID --channel stable|beta --to N --feed DIR --feed-key BASE64 --work DIR
#                [--from N-1] [--artifact FILE] [--expect-delta]
#   --feed          the release folder of N, its feed signed (tools/desktop-update-signing):
#                   the signature must hold against --feed-key and the packages match the
#                   feed (Node runs the check).
#   --feed-key      the Ed25519 key N must trust: its --smoke report shows it.
#   --from          the installed N-1 to update; without it (first release of a channel), the
#                   gate checks that the installed N starts: --smoke shows N.
#   --artifact      installs first, from a portable zip (win, osx) or an AppImage (linux);
#                   without it, the install already under WORK/install is used.
#   --expect-delta  N must be applied from deltas: the updater's cache holds N-1 (a second
#                   hop, after a first update).
# Reports: WORK/logs/{before,apply,after,start,tampered}-<versions>.out (smoke reports) and
# .err (JSON logs and updater output).
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

readonly UPDATER_TIMEOUT_SECONDS=180
readonly SMOKE_ATTEMPTS=3

rid="" channel="" from="" to="" feed="" feed_key="" work="" artifact="" expect_delta=false
while [ $# -gt 0 ]; do
  case "$1" in
    --rid) rid="$2"; shift 2 ;;
    --channel) channel="$2"; shift 2 ;;
    --from) from="$2"; shift 2 ;;
    --to) to="$2"; shift 2 ;;
    --feed) feed="$2"; shift 2 ;;
    --feed-key) feed_key="$2"; shift 2 ;;
    --work) work="$2"; shift 2 ;;
    --artifact) artifact="$2"; shift 2 ;;
    --expect-delta) expect_delta=true; shift ;;
    *) lodb_fail "gate.sh: unknown argument '$1'" ;;
  esac
done
for required in rid channel to feed feed_key work; do
  [ -n "${!required}" ] || lodb_fail "gate.sh: --${required//_/-} is required"
done
lodb_check_rid "$rid"
velopack_channel="$(lodb_velopack_channel "$rid" "$channel")"
feed_name="releases.$velopack_channel.json"
signing="$(lodb_repo_root)/tools/desktop-update-signing"

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

# The feed N is installed from is the signed one, and lists the folder's packages.
check_feed() {
  node "$signing/verify-feed.mjs" --releases "$feed" --channel "$velopack_channel" \
    --public-key "$feed_key" --packages || lodb_fail "the feed of $feed does not hold"
}

expect_feed_key() {
  grep -qxF -- "feed-key: ed25519 $feed_key" "$logs/$1.out" \
    || lodb_fail "$1: $to does not trust the feed key $feed_key"
}

start_only() {
  lodb_log "no N-1: smoke of the installed version ($to) only"
  smoke "start-$to" || lodb_fail "start-$to: --smoke exited with $?"
  expect_report "start-$to" "$to" none
  expect_feed_key "start-$to"
}

update_from_previous() {
  local before="before-$from" apply="apply-$from-$to" after="after-$to" attempt
  lodb_log "smoke of the installed version ($from)"
  smoke "$before" || lodb_fail "$before: --smoke exited with $?"
  expect_report "$before" "$from" none

  lodb_log "update $from -> $to from $feed"
  LODB_DESKTOP_UPDATE_SOURCE="$(lodb_native_path "$feed")" smoke "$apply" --apply-updates \
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
  expect_feed_key "$after"
}

# N ignores its own feed once a single byte was added after signing: it stays on N and logs
# the refusal, where a build that verifies nothing would read the feed as valid.
expect_tampered_feed_refused() {
  local name="tampered-$to" tampered="$work/tampered"
  rm -rf "$tampered" && mkdir -p "$tampered"
  cp "$feed/$feed_name" "$feed/$feed_name.sig" "$tampered/"
  printf ' ' >>"$tampered/$feed_name"
  lodb_log "update check of $to against a tampered feed"
  LODB_DESKTOP_UPDATE_SOURCE="$(lodb_native_path "$tampered")" smoke "$name" --apply-updates \
    || lodb_fail "$name: --smoke --apply-updates exited with $?"
  expect_report "$name" "$to" none
  grep -qF '"Refusal":"InvalidSignature"' "$logs/$name.err" \
    || lodb_fail "$name: $to did not refuse a feed whose signature no longer holds"
}

deltas=""
check_feed
[ -z "$artifact" ] || install_from "$artifact"
if [ -z "$from" ]; then
  start_only
else
  update_from_previous
fi
expect_tampered_feed_refused
if [ -z "$from" ]; then
  lodb_log "gate passed: $to starts on $rid ($channel), no update to check"
else
  lodb_log "gate passed: $from -> $to on $rid ($channel), ${deltas:-0} delta(s)"
fi
