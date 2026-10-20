#!/usr/bin/env bash
# The `docker` that scaleway-bucket.sh sees on the bench (scaleway-bucket.test.mjs): records
# each call and the access key the call inherits, then runs the container on the bench's
# network, where Scaleway's address names moto.
set -euo pipefail

printf '%s\n' "$*" >>"$TRACE"
printf '%s\n' "${AWS_ACCESS_KEY_ID:-}" >>"$TRACE.keys"
arguments=()
for argument in "$@"; do
  arguments+=("${argument//https:\/\/s3.fr-par.scw.cloud/http://moto:5000}")
done
if [[ "${arguments[0]}" == run ]]; then
  exec "$REAL_DOCKER" run --network "$NETWORK" "${arguments[@]:1}"
fi
exec "$REAL_DOCKER" "${arguments[@]}"
