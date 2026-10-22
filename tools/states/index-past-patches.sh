#!/usr/bin/env bash
# Indexes the patches before the latest one on a started lodb-dev stack, for the E2E suite and
# tools/routing/check-urls.sh (docs/produit/seo-une-page-par-etat.md, § 8). A fresh stack ingests
# its latest patch alone, so its index of states covers that patch only: every older page falls
# under the residual rule, and no state is ever closed. This ingests the en_US datasets of the
# patches listed just before the latest (`ingest --languages en_US`, which never promotes), then
# runs the `states` service again: its build sees the coverage start moved and indexes them all
# in a new generation, which the API reloads within a minute.
#
#   tools/states/index-past-patches.sh [base-url]
#
# base-url defaults to the integration stack (http://localhost:18080); a slot passes its own
# nginx, for instance http://localhost:18180, and names its project in LODB_E2E_COMPOSE_PROJECT,
# as its E2E do (lodb-dev by default). LODB_STATES_PATCHES sets how many patches (5 by
# default: enough for states closed after several patches). The script first waits for the
# patch watch to promote a version (LODB_ROUTING_WAIT seconds, 600 by default). Needs curl, jq
# and docker. Each patch takes about a minute, its images included, which its pages need anyway.
set -euo pipefail

readonly BASE="${1:-http://localhost:18080}"
readonly PROJECT="${LODB_E2E_COMPOSE_PROJECT:-lodb-dev}"
readonly PATCHES="${LODB_STATES_PATCHES:-5}"
readonly WAIT="${LODB_ROUTING_WAIT:-600}"
readonly POLL=5
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
readonly ROOT

compose() {
  docker compose -p "$PROJECT" -f "$ROOT/compose.yaml" -f "$ROOT/compose.override.yaml" "$@"
}

meta() {
  curl -sS --max-time 30 "$BASE/api/meta"
}

# The messages of the JSON log lines of one event, the others dropped.
messages_of() {
  jq -Rr --arg event "$1" 'fromjson? | select(.EventName == $event) | .Message'
}

deadline=$((SECONDS + WAIT))
until latest="$(meta | jq -er '.latest // empty' 2>/dev/null)"; do
  if [ "$SECONDS" -ge "$deadline" ]; then
    echo "No version promoted after ${WAIT}s: /api/meta has no latest version." >&2
    exit 1
  fi
  sleep "$POLL"
done

versions="$(meta | jq -r --arg latest "$latest" --argjson count "$PATCHES" \
  '.versions as $v | ($v | index($latest)) as $at | $v[$at + 1:$at + 1 + $count][]')"
for version in $versions; do
  compose exec -T api dotnet LoDb.Api.dll ingest --version "$version" --languages en_US |
    messages_of ingest.version.completed
done
compose run --rm --no-deps states | messages_of states.update.completed
