#!/usr/bin/env bash
# Usage: bash heavy.sh <command...>
# Machine-wide semaphore for the memory-heavy commands that parallel agents run (ng test,
# ng build, playwright, dotnet build/test): at most HEAVY_SLOTS (default 2) run at once
# across all agents. Waits for a free slot, runs the command with capped parallelism, frees
# the slot on exit. A slot whose owner died, or older than 20 minutes, is reclaimed.
LOCKS='C:/Users/charl/AppData/Local/Temp/claude/F--Git-LeagueOfDataBaseFinal/8504f518-b0ba-4f9d-be95-f2bbdcc96c3e/scratchpad/cmp/locks'
SLOTS=${HEAVY_SLOTS:-2}
STALE_SECONDS=1200
mkdir -p "$LOCKS"

# Caps that the wrapped command inherits.
export VITEST_MAX_WORKERS=3            # default is one jsdom worker per logical CPU (24)
export MSBUILDDISABLENODEREUSE=1       # no resident MSBuild nodes after a build
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export UseSharedCompilation=false      # no resident VBCSCompiler

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

SLOT=''
while [ -z "$SLOT" ]; do
  for i in $(seq 1 "$SLOTS"); do
    claim "$i" && break
  done
  [ -z "$SLOT" ] && sleep 3
done
trap 'rm -rf "$SLOT"' EXIT INT TERM
"$@"
