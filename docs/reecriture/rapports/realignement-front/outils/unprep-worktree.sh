#!/usr/bin/env bash
# Usage: bash unprep-worktree.sh <worktree root>
# Removes the junctions made by prep-worktree.sh WITHOUT touching their targets (rmdir on a
# junction deletes the link only), and restores the changelog symlink file. Run it before any
# `git worktree remove`, which could otherwise recurse through a junction.
set -euo pipefail
cd "$1"
W="$(pwd -W)"
P=src/LoDb.Web/src/app/features/editorial/changelog/published
for J in src/LoDb.Web/node_modules tests/LoDb.E2E/node_modules "$P"; do
  if [ -d "$J" ] && fsutil reparsepoint query "$(cygpath -w "$W/$J")" >/dev/null 2>&1; then
    cmd //c rmdir "$(cygpath -w "$W/$J")"
  fi
done
git update-index --no-skip-worktree "$P"
git checkout -- "$P"
echo "unprepared $W"
