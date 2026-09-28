#!/usr/bin/env bash
# Version of a desktop release, from the git tags of the current repository (ADR 0008: the
# tag is the only source, and VersionPrefix must agree with it).
#   stable  the desktop-vX.Y.Z tag on SHA gives the version; no tag, no release; a tag that
#           differs from VersionPrefix fails.
#   beta    VersionPrefix-beta.N, N one above the highest beta tag of that prefix.
#
# Usage: release-version.sh --channel stable|beta --sha SHA --prefix VERSION_PREFIX
# Prints, for $GITHUB_OUTPUT: release=true|false, version=, tag=, and reason= when there
# is no release.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

readonly STABLE_VERSION='^[0-9]+\.[0-9]+\.[0-9]+$'

channel="" sha="" prefix=""
while [ $# -gt 0 ]; do
  case "$1" in
    --channel) channel="$2"; shift 2 ;;
    --sha) sha="$2"; shift 2 ;;
    --prefix) prefix="$2"; shift 2 ;;
    *) lodb_fail "release-version.sh: unknown argument '$1'" ;;
  esac
done
for required in channel sha prefix; do
  [ -n "${!required}" ] || lodb_fail "release-version.sh: --$required is required"
done
[[ "$prefix" =~ $STABLE_VERSION ]] || lodb_fail "VersionPrefix '$prefix' is not X.Y.Z"

tag_prefix="$(lodb_release_tag "")"

emit() {
  printf 'release=true\nversion=%s\ntag=%s\n' "$1" "$(lodb_release_tag "$1")"
}

# Only X.Y.Z tags: the beta tags of the same commit are the workflow's own.
stable_version() {
  local tags=() tag
  while IFS= read -r tag; do
    [[ "${tag#"$tag_prefix"}" =~ $STABLE_VERSION ]] && tags+=("$tag")
  done < <(git tag --points-at "$sha" --list "$tag_prefix*")
  case "${#tags[@]}" in
    0) printf 'release=false\nreason=no %sX.Y.Z tag on %s\n' "$tag_prefix" "$sha"
      return ;;
    1) ;;
    *) lodb_fail "several release tags on $sha: ${tags[*]}" ;;
  esac
  local version="${tags[0]#"$tag_prefix"}"
  [ "$version" = "$prefix" ] \
    || lodb_fail "tag ${tags[0]} and VersionPrefix $prefix diverge: bump one of them"
  emit "$version"
}

beta_version() {
  local highest=0 tag number
  while IFS= read -r tag; do
    number="${tag#"$tag_prefix$prefix-beta."}"
    if [[ "$number" =~ ^[0-9]+$ ]] && [ "$number" -gt "$highest" ]; then
      highest="$number"
    fi
  done < <(git tag --list "$tag_prefix$prefix-beta.*")
  emit "$prefix-beta.$((highest + 1))"
}

case "$channel" in
  stable) stable_version ;;
  beta) beta_version ;;
  *) lodb_fail "unknown channel '$channel' (stable or beta)" ;;
esac
