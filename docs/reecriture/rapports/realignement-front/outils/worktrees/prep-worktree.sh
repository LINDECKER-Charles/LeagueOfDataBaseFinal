#!/usr/bin/env bash
# Usage: bash prep-worktree.sh <worktree root>
# Makes a git worktree of the repo able to build and test the Angular front locally on Windows:
# - src/LoDb.Web/node_modules and tests/LoDb.E2E/node_modules -> junctions to the main
#   checkout's (no npm ci per worktree);
# - the changelog symlink (checked out as a text file, core.symlinks=false) -> junction to
#   the worktree's app/public/changelog, hidden from git with skip-worktree.
# Undo with unprep-worktree.sh BEFORE any `git worktree remove`.
set -euo pipefail
cd "$1"
W="$(pwd -W)"
MAIN='F:/Git/LeagueOfDataBaseFinal'
P=src/LoDb.Web/src/app/features/editorial/changelog/published
if [ ! -d "$P" ]; then
  git update-index --skip-worktree "$P"
  rm -f "$P"
  cmd //c mklink //J "$(cygpath -w "$W/$P")" "$(cygpath -w "$W/app/public/changelog")" >/dev/null
fi
for M in src/LoDb.Web/node_modules tests/LoDb.E2E/node_modules; do
  if [ ! -e "$M" ]; then
    cmd //c mklink //J "$(cygpath -w "$W/$M")" "$(cygpath -w "$MAIN/$M")" >/dev/null
  fi
done
echo "prepared $W"
