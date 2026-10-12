#!/usr/bin/env bash
# Turns the maintenance page on and off on a host (docker/nginx/server.d/maintenance.conf),
# around the step of the deploy job that replaces the containers (.github/workflows/_deploy.yml):
#
#   bash tools/deploy/maintenance.sh start DIR   # every page answers the maintenance page
#   bash tools/deploy/maintenance.sh end DIR     # the site again; the window's length is kept
#
# DIR is .deploy/maintenance, which compose.deploy.yaml mounts read-only in nginx. `start` writes
# DIR/active.json, the switch nginx reads and the state the page polls:
#
#   {"startedAt":<Unix time, in seconds>,"expectedSeconds":<seconds>}
#
# expectedSeconds is the length of the previous window, which `end` keeps in DIR/last-duration
# once the containers serve again. Without one (the first deploy, a duration unreadable), the key
# is left out and the page shows the usual duration. The value lives on the host, never on
# GitHub: the estimate keeps working whoever can read the repository.
#
# `end` without a window on does nothing. A deploy that stops before `end` leaves the page on:
# the stack it leaves may not serve. Removing active.json turns it off by hand.
#
# Each file is replaced by a rename (a temporary file in DIR, then mv): nginx and the page never
# read half of one. World-readable, in a world-readable folder: nginx runs as its own user.
set -euo pipefail
shopt -s inherit_errexit

readonly ACTIVE=active.json
readonly LAST_DURATION=last-duration
readonly USAGE='usage: bash tools/deploy/maintenance.sh start|end DIR'
readonly SECONDS_NUMBER='^[1-9][0-9]*$'

fail() {
  echo "maintenance.sh: $*" >&2
  exit 1
}

# Writes its input to DIR/NAME in one rename, mode 644 (mktemp creates 600).
publish() {
  local dir="$1" name="$2" staged
  staged="$(mktemp "$dir/.$name.XXXXXX")"
  cat > "$staged"
  chmod 644 "$staged"
  mv -f "$staged" "$dir/$name"
}

# The previous window's length in seconds, or nothing when there is none worth reading.
last_duration() {
  local value=''
  if [ -f "$1/$LAST_DURATION" ]; then
    value="$(tr -d '[:space:]' < "$1/$LAST_DURATION")"
  fi
  if [[ "$value" =~ $SECONDS_NUMBER ]]; then
    echo "$value"
  fi
}

start_window() {
  local dir="$1" expected now
  install -d -m 755 "$dir"
  expected="$(last_duration "$dir")"
  now="$(date +%s)"
  if [ -n "$expected" ]; then
    printf '{"startedAt":%s,"expectedSeconds":%s}\n' "$now" "$expected" | publish "$dir" "$ACTIVE"
    echo "Maintenance page on; the previous deploy took ${expected} s."
  else
    printf '{"startedAt":%s}\n' "$now" | publish "$dir" "$ACTIVE"
    echo "Maintenance page on; no previous deploy measured, the page shows the usual duration."
  fi
}

end_window() {
  local dir="$1" started now duration
  if [ ! -f "$dir/$ACTIVE" ]; then
    return 0
  fi
  started="$(sed -nE 's/.*"startedAt":([0-9]+).*/\1/p' "$dir/$ACTIVE")"
  rm -f "$dir/$ACTIVE"
  if [[ ! "$started" =~ $SECONDS_NUMBER ]]; then
    echo "Maintenance page off; its start was unreadable, the previous duration is kept."
    return 0
  fi
  now="$(date +%s)"
  # At least a second: a window of 0 would read as no measure at all.
  duration=$(( now > started ? now - started : 1 ))
  echo "$duration" | publish "$dir" "$LAST_DURATION"
  echo "Maintenance page off after ${duration} s."
}

main() {
  [ "$#" -eq 2 ] || fail "$USAGE"
  case "$1" in
    start) start_window "$2" ;;
    end) end_window "$2" ;;
    *) fail "$USAGE" ;;
  esac
}

main "$@"
