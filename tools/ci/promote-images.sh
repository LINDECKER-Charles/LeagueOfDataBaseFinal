#!/usr/bin/env bash
# Promotes the images staging validated to the production tags, by digest (_promote.yml):
#
#   IMAGE_PREFIX=ghcr.io/<owner>/lodb bash tools/ci/promote-images.sh
#
# Optional: SOURCE_TAG (staging), VALIDATED_TAG (staging-validated), TARGET_TAGS ("prod latest"),
# IMAGES ("api web-ssr nginx"). Run from a full-history checkout of the commit to promote.
#
# For each image, the digest SOURCE_TAG names must be the one VALIDATED_TAG names, which ci.yml
# sets once staging served it and passed its smoke tests: a build pushed since, or a staging
# deploy that failed or still runs, stops the promotion. The images must come from one revision
# (their OCI revision label), whose code is the code of HEAD on every path the pipeline builds
# or deploys: otherwise prod would run code that staging never served. Only then are the target
# tags created, from the digests themselves (tag-image.sh).
set -euo pipefail

readonly REVISION_LABEL=org.opencontainers.image.revision
# The `stack-paths` of ci.yml, the paths a change of which the pipeline builds, tests and
# deploys (a test keeps both lists equal). A path outside them (docs) may differ.
readonly STACK_PATHS=(
  src tests tools docker 'compose*.yaml' LoDb.slnx 'Directory.*' global.json .github/workflows
)
readonly DIGEST_FORMAT='^sha256:[0-9a-f]{64}$'
TAG_IMAGE="$(dirname "${BASH_SOURCE[0]}")/tag-image.sh"
readonly TAG_IMAGE
: "${IMAGE_PREFIX:?IMAGE_PREFIX is required (ghcr.io/<owner>/lodb)}"
SOURCE_TAG="${SOURCE_TAG:-staging}"
VALIDATED_TAG="${VALIDATED_TAG:-staging-validated}"
read -r -a target_tags <<< "${TARGET_TAGS:-prod latest}"
read -r -a images <<< "${IMAGES:-api web-ssr nginx}"

fail() {
  echo "promote-images: $*" >&2
  exit 1
}

# "<digest> <revision>" of a reference, from one resolution of it. The images are built for
# one platform, so .Image is a single configuration (an index of several would fail here).
describe() {
  docker buildx imagetools inspect "$1" \
    --format "{{.Manifest.Digest}} {{index .Image.Config.Labels \"$REVISION_LABEL\"}}"
}

# Prints "<digest> <revision>" of an image once its source tag is the validated digest.
validated_image() {
  local image="$1" source validated digest revision
  source="$(describe "$IMAGE_PREFIX/$image:$SOURCE_TAG")" \
    || fail "$image: no $SOURCE_TAG image to promote."
  validated="$(describe "$IMAGE_PREFIX/$image:$VALIDATED_TAG")" \
    || fail "$image: no $VALIDATED_TAG image: staging has validated no build yet."
  read -r digest revision <<< "$source"
  [[ "$digest" =~ $DIGEST_FORMAT ]] || fail "$image: unreadable digest '$digest'."
  [ "$source" = "$validated" ] \
    || fail "$image: $SOURCE_TAG ($source) is not what staging validated ($validated): a" \
      "build is under way or its deployment failed. Promote once staging is green again."
  [ -n "$revision" ] || fail "$image: $digest has no $REVISION_LABEL label."
  echo "$digest $revision"
}

# Fails unless the revision staging serves holds the code of HEAD on the stack's paths.
check_code() {
  local revision="$1" head
  head="$(git rev-parse HEAD)"
  git cat-file -e "$revision^{commit}" 2>/dev/null \
    || fail "staging serves $revision, a commit this checkout does not have."
  if ! git diff --quiet "$revision" "$head" -- "${STACK_PATHS[@]}"; then
    git diff --stat "$revision" "$head" -- "${STACK_PATHS[@]}" >&2
    fail "staging serves $revision, whose code differs from $head (above): merge test into" \
      "main again once staging serves the revision to promote."
  fi
}

declare -A digests
revision=""
for image in "${images[@]}"; do
  # Its own shell: a failure there has printed why, and stops the promotion here.
  described="$(validated_image "$image")" || exit 1
  read -r digest image_revision <<< "$described"
  revision="${revision:-$image_revision}"
  [ "$image_revision" = "$revision" ] \
    || fail "staging mixes two builds: $image is $image_revision, not $revision."
  digests[$image]="$digest"
done
check_code "$revision"

for image in "${images[@]}"; do
  bash "$TAG_IMAGE" "$IMAGE_PREFIX/$image" "${digests[$image]}" "${target_tags[@]}"
done
echo "promote-images: revision $revision promoted to ${target_tags[*]}."
