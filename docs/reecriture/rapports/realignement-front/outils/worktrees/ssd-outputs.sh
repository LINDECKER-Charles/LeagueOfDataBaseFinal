#!/usr/bin/env bash
# Usage: bash ssd-outputs.sh <checkout or worktree root> <name>
# Moves the write-heavy, git-ignored outputs of a checkout (Angular dist, Playwright
# test-results) off the F: spinning drive: each becomes an NTFS junction to a folder on the C:
# NVMe drive. An existing folder is renamed then deleted at low priority; a folder in use (a
# preview serving it) cannot be renamed and is left as is. unprep-worktree.sh removes the
# junctions (never their targets) before any `git worktree remove`.
set -euo pipefail
ROOT="$(cd "$1" && pwd -W)"
SSD="C:/Users/charl/AppData/Local/Temp/lodb-parite-ssd/$2"
for OUT in src/LoDb.Web/dist tests/LoDb.E2E/test-results; do
  path="$ROOT/$OUT"
  if [ -e "$path" ] && fsutil reparsepoint query "$(cygpath -w "$path")" >/dev/null 2>&1; then
    echo "already a junction: $path"
    continue
  fi
  target="$SSD/$(basename "$OUT")"
  mkdir -p "$target"
  if [ -e "$path" ]; then
    if ! mv "$path" "$path.hdd-old" 2>/dev/null; then
      echo "in use, left on F: $path"
      continue
    fi
    (nice -n 19 rm -rf "$path.hdd-old" &)
  fi
  cmd //c mklink //J "$(cygpath -w "$path")" "$(cygpath -w "$target")" >/dev/null
  echo "junction: $path -> $target"
done
