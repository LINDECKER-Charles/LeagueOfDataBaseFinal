#!/usr/bin/env bash
# Pre-ingestion (plan-migration.md, cutover step 3; L8.2): fills the new stack's storage volume
# and its manifest before the switch, with the latest Data Dragon versions in every language.
# The long tail stays on demand.
#
#   tools/next/cutover/pre-ingest.sh [--latest 3] [--] [docker compose options…]
#
# The compose options name the stack, the integration one by default:
#   -p lodb-next -f compose.next.yaml -f compose.next.override.yaml
# The served host passes its own, for instance:
#   tools/next/cutover/pre-ingest.sh -- -p lodb-prod-next -f compose.next.yaml -f compose.next.deploy.yaml
#
# Runs `ingest --latest <n> --languages all` in a one-shot container of the api service
# (--no-deps: the database must already be migrated), which shares the stack's storage volume.
# Run it with the api stopped, or at least before its patch watch starts on a new version:
# a version held by another run ends in code 1 (ingest.version.locked). Code 1 (a version
# incomplete: locked, or an image Data Dragon refused) starts one second pass, which keeps
# what is already stored. Exit code of the last pass: 0 when every version completed.
set -euo pipefail

latest=3
while [ $# -gt 0 ]; do
  case "$1" in
    --latest) latest="$2"; shift 2 ;;
    --) shift; break ;;
    *) break ;;
  esac
done
if [ $# -eq 0 ]; then
  set -- -p lodb-next -f compose.next.yaml -f compose.next.override.yaml
fi

cd "$(dirname "$0")/../../.."
status=0
for pass in 1 2; do
  started=$SECONDS
  status=0
  docker compose "$@" run --rm --no-deps api ingest --latest "$latest" --languages all || status=$?
  echo "ingest --latest $latest --languages all, pass $pass: code $status in $((SECONDS - started)) s"
  if [ "$status" != 1 ]; then
    break
  fi
done
exit "$status"
