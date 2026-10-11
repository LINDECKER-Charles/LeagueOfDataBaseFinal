#!/usr/bin/env bash
# Points tags of an image repository at one digest, then checks that each tag names it:
#
#   bash tools/ci/tag-image.sh <repository> <sha256:digest> <tag>...
#
# The tags are created from `<repository>@<digest>`, never from another tag, so that what they
# name cannot change between the read and the write; `imagetools create` copies the index as
# it is (attestations included) and the check after it proves the digest did not change.
# Callers: _build.yml (the moving tag, once the scan passed), ci.yml (the digests staging
# validated) and promote-images.sh (the production tags).
set -euo pipefail

readonly DIGEST_FORMAT='^sha256:[0-9a-f]{64}$'
# The reference grammar of a tag (distribution spec).
readonly TAG_FORMAT='^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$'

fail() {
  echo "tag-image: $*" >&2
  exit 1
}

[ "$#" -ge 3 ] || fail "usage: tag-image.sh <repository> <sha256:digest> <tag>..."
repository="$1"
digest="$2"
shift 2
[[ "$digest" =~ $DIGEST_FORMAT ]] || fail "'$digest' is not a sha256 digest."
tag_flags=()
for tag in "$@"; do
  [[ "$tag" =~ $TAG_FORMAT ]] || fail "'$tag' is not a tag."
  tag_flags+=(--tag "$repository:$tag")
done

docker buildx imagetools create "${tag_flags[@]}" "$repository@$digest"
for tag in "$@"; do
  named="$(docker buildx imagetools inspect "$repository:$tag" --format '{{.Manifest.Digest}}')"
  [ "$named" = "$digest" ] || fail "$repository:$tag names $named, not $digest."
done
echo "$repository@$digest is now tagged: $*"
