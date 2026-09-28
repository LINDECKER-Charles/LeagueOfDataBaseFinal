#!/usr/bin/env bash
# Usage: bash heavy.sh <command...>
# Machine-wide semaphore for the memory- and CPU-heavy commands that parallel agents run
# (ng test, ng build, lint, typecheck, playwright, dotnet build/test): at most HEAVY_SLOTS
# (default 2) run at once across all agents, only while enough RAM is free, at below-normal
# priority so the user's own applications stay responsive, not while the F: drive is
# saturated, and not at all while the user plays a League of Legends match. A slot whose owner died, or older
# than 20 minutes, is reclaimed.
LOCKS='C:/Users/charl/AppData/Local/Temp/claude/F--Git-LeagueOfDataBaseFinal/8504f518-b0ba-4f9d-be95-f2bbdcc96c3e/scratchpad/cmp/locks'
SLOTS=${HEAVY_SLOTS:-2}
MIN_FREE_KB=$(( ${HEAVY_MIN_FREE_GB:-12} * 1024 * 1024 ))
STALE_SECONDS=1200
mkdir -p "$LOCKS"

# Caps that the wrapped command inherits.
export VITEST_MAX_WORKERS=3            # default is one jsdom worker per logical CPU (24)
export NG_BUILD_MAX_WORKERS=4          # Angular build worker pool (default: CPU count)
export GOMAXPROCS=6                    # esbuild threads
export MSBUILDDISABLENODEREUSE=1       # no resident MSBuild nodes after a build
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export UseSharedCompilation=false      # no resident VBCSCompiler

free_kb() { awk '/^MemFree:/ { print $2 }' /proc/meminfo; }

# F: (repo, worktrees, node_modules) is a spinning SMR drive: random writes on it froze the
# whole machine in the previous run. watch-disk.ps1 keeps this flag at 1 while it is saturated.
# A stale flag (sampler stopped) is ignored, and the wait is capped so no agent hangs forever.
DISK_FLAG="$(dirname "$LOCKS")/disk-busy"
DISK_WAIT_CAP=300
WAIT_START=$(date +%s)
disk_busy() {
  [ -f "$DISK_FLAG" ] || return 1
  [ $(( $(date +%s) - $(stat -c %Y "$DISK_FLAG") )) -lt 60 ] || return 1
  [ $(( $(date +%s) - WAIT_START )) -lt "$DISK_WAIT_CAP" ] || return 1
  [ "$(cat "$DISK_FLAG")" = 1 ]
}

claim() {
  local slot="$LOCKS/slot$1"
  if mkdir "$slot" 2>/dev/null; then
    echo "$$" > "$slot/pid"
    SLOT="$slot"
    return 0
  fi
  local pid age
  pid=$(cat "$slot/pid" 2>/dev/null)
  age=$(( $(date +%s) - $(stat -c %Y "$slot" 2>/dev/null || date +%s) ))
  if { [ -n "$pid" ] && ! kill -0 "$pid" 2>/dev/null; } || [ "$age" -gt "$STALE_SECONDS" ]; then
    rm -rf "$slot"
  fi
  return 1
}

# Game mode: the user plays League of Legends, installed on the same physical drive as F:.
# While a match runs (its own process, not the launcher), nothing heavy starts, without cap.
game_running() { ps -W 2>/dev/null | grep -q 'League of Legends\.exe'; }

REASON=''
say_once() {
  [ "$REASON" = "$1" ] && return
  REASON=$1
  echo "heavy.sh: $2" >&2
}

SLOT=''
while [ -z "$SLOT" ]; do
  if game_running; then
    say_once game "waiting: the user is playing a League of Legends match on this machine (game mode). The command starts by itself when the match ends. This is expected: never bypass heavy.sh; wait, or do light work (reading, editing) meanwhile."
    WAIT_START=$(date +%s)
  elif [ "$(free_kb)" -lt "$MIN_FREE_KB" ]; then
    say_once ram "waiting: less than ${HEAVY_MIN_FREE_GB:-12} GB of free RAM."
  elif disk_busy; then
    say_once disk "waiting: the F: drive is saturated (at most $(( DISK_WAIT_CAP / 60 )) min)."
  else
    for i in $(seq 1 "$SLOTS"); do
      claim "$i" && break
    done
  fi
  [ -z "$SLOT" ] && sleep 5
done
trap 'rm -rf "$SLOT"' EXIT INT TERM
nice -n 10 "$@"
