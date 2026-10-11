#!/usr/bin/env bash
# `docker buildx imagetools` against a registry kept in the folder $FAKE_REGISTRY, for the tests
# of promote-images.sh and tag-image.sh: <repository, slashes as _>/tags/<tag> holds a digest,
# <repository>/blobs/<hex> the revision label of the image that digest names.
# FAKE_CREATE_DIGEST makes `create` store another digest than its source's.
set -euo pipefail

folder() { echo "$FAKE_REGISTRY/$(printf '%s' "$1" | tr '/' '_')"; }

repository_of() {
  if [[ "$1" == *@* ]]; then echo "${1%@*}"; else echo "${1%:*}"; fi
}

# Digest a reference names; fails when the registry holds none.
resolve() {
  if [[ "$1" == *@* ]]; then
    [ -f "$(folder "${1%@*}")/blobs/${1##*:}" ] || return 1
    echo "${1#*@}"
  else
    cat "$(folder "${1%:*}")/tags/${1##*:}" 2>/dev/null
  fi
}

[ "$1 $2" = "buildx imagetools" ] || { echo "unexpected: docker $*" >&2; exit 2; }
case "$3" in
  inspect)
    digest="$(resolve "$4")" || { echo "ERROR: $4: not found" >&2; exit 1; }
    if [[ "$6" == *Labels* ]]; then
      echo "$digest $(cat "$(folder "$(repository_of "$4")")/blobs/${digest#sha256:}")"
    else
      echo "$digest"
    fi
    ;;
  create)
    shift 3
    tags=()
    while [ "$1" = --tag ]; do tags+=("$2"); shift 2; done
    digest="$(resolve "$1")" || { echo "ERROR: $1: not found" >&2; exit 1; }
    for tag in "${tags[@]}"; do
      echo "${FAKE_CREATE_DIGEST:-$digest}" > "$(folder "${tag%:*}")/tags/${tag##*:}"
    done
    ;;
  *) echo "unexpected: docker $*" >&2; exit 2 ;;
esac
