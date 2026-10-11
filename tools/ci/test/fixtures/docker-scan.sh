#!/usr/bin/env bash
# `docker` for the tests of the scan step of _build.yml. `pull` does nothing, `save --output`
# writes an empty archive, and `run` writes to $FAKE_SCAN_RECORD what the scanner would see:
# `workdir <folder>`; per mount, `mount <host path> <container path>` then one
# `file <container path>` per file it shows; then `args <the scanner's arguments>`.
# It exits $FAKE_SCAN_STATUS (0 by default).
set -euo pipefail
shopt -s nullglob dotglob

describe_mount() {
  local spec="${1%:ro}"
  spec="${spec%:rw}"
  local source="${spec%:*}" target="${spec##*:}" file
  echo "mount $source $target"
  if [ ! -d "$source" ]; then
    echo "file $target"
    return
  fi
  for file in "$source"/*; do
    echo "file $target/${file##*/}"
  done
}

case "$1" in
  pull) ;;
  save)
    [ "$2" = --output ] || { echo "unexpected: docker $*" >&2; exit 2; }
    : > "$3"
    ;;
  run)
    shift
    workdir=/
    mounts=()
    while [[ "$1" == -* ]]; do
      case "$1" in
        --rm) shift ;;
        -v) mounts+=("$2"); shift 2 ;;
        -w) workdir="$2"; shift 2 ;;
        *) echo "unexpected flag: docker run $1" >&2; exit 2 ;;
      esac
    done
    shift # the scanner's image
    {
      echo "workdir $workdir"
      for mount in "${mounts[@]}"; do describe_mount "$mount"; done
      echo "args $*"
    } > "$FAKE_SCAN_RECORD"
    exit "${FAKE_SCAN_STATUS:-0}"
    ;;
  *) echo "unexpected: docker $*" >&2; exit 2 ;;
esac
