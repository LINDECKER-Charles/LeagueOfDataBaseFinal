#!/usr/bin/env bash
# Sets up, or checks and repairs, the Scaleway bucket of the pre-rendered pages (ADR 0011),
# then proves from the VPS that nginx can read it. Idempotent: a replay only touches what
# differs from the expected state, and says what it found. Operator guide: README.md here.
#
#   bash tools/prerender/scaleway-bucket.sh [configurer]         # workstation, operator key
#   bash tools/prerender/scaleway-bucket.sh verifier             # VPS, writer key
#   bash tools/prerender/scaleway-bucket.sh supprimer-politique  # workstation, recovery
#
# The AWS CLI runs in its official image, pinned, which also brings jq and curl: Docker is
# the only dependency, and bash 3.2 (macOS) is enough. Keys travel through the environment
# (`-e NAME` without a value), never through argv, and are never printed. Messages are French
# without accents, like the operator's other scripts (infra-vps/sauvegardes).
# shellcheck source-path=SCRIPTDIR
set -euo pipefail

# Pinned by digest, its tag kept for the reader; more than two weeks old when pinned.
readonly AWS_CLI_DIGEST=sha256:c240dd63cdbab17197486d55460675ef2b94c9cc372dedae34dee53ccd30657c
readonly IMAGE_AWS_CLI="amazon/aws-cli:2.36.50@$AWS_CLI_DIGEST"
# IAM grants take up to 5 minutes to apply: a 403 is retried that long before it counts.
readonly ACCESS_ATTEMPTS=10
readonly ACCESS_RETRY_SECONDS=30
# A dropped route to Scaleway fails a call instead of hanging the run.
readonly CLI_CONNECT_TIMEOUT_SECONDS=10
readonly CLI_READ_TIMEOUT_SECONDS=60
readonly BUCKET_FORMAT='^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$'
readonly REGION_FORMAT='^[a-z]{2}-[a-z]{3}$'
readonly UUID_FORMAT='^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
# Epoch 0 holds the probes only: its retirement rule is always in place, so a probe cleans
# itself up.
readonly PROBE_EPOCH=0
readonly SCRIPT=tools/prerender/scaleway-bucket.sh
readonly OPERATOR_KEY_HINT="la cle API temporaire de l'operateur (proprietaire de l'organisation)"
LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib"
readonly LIB_DIR
# The S3 endpoint of REGION, set once the inputs are checked.
endpoint=""

fail() {
  printf 'Erreur : %s\n' "$1" >&2
  exit 1
}

# The last line of an AWS CLI answer: its error message, which holds no key.
last_line() {
  printf '%s\n' "$1" | tail -n 1
}

is_denied() {
  case $1 in *"(403)"* | *Forbidden* | *AccessDenied*) return 0 ;; *) return 1 ;; esac
}

# require NAME FORMAT HINT: the variable NAME matches FORMAT, or the run stops before any call.
require() {
  [[ "${!1:-}" =~ $2 ]] || fail "$1 : $3"
}

s3api() {
  s3api_with_volume "" "$@"
}

# The AWS CLI against Scaleway, with VOLUME mounted on /sonde when given. The checksums that
# AWS CLI 2.23+ adds by default are kept to the operations that require them.
s3api_with_volume() {
  local volume=$1
  shift
  docker run --rm -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY -e "AWS_DEFAULT_REGION=$REGION" \
    -e AWS_DEFAULT_OUTPUT=json -e AWS_REQUEST_CHECKSUM_CALCULATION=when_required \
    -e AWS_RESPONSE_CHECKSUM_VALIDATION=when_required ${volume:+-v "$volume:/sonde:ro"} \
    "$IMAGE_AWS_CLI" s3api --endpoint-url "$endpoint" \
    --cli-connect-timeout "$CLI_CONNECT_TIMEOUT_SECONDS" \
    --cli-read-timeout "$CLI_READ_TIMEOUT_SECONDS" "$@" </dev/null
}

jq_in_image() {
  docker run --rm -i --entrypoint jq "$IMAGE_AWS_CLI" "$@"
}

# retry_while_denied WHAT COMMAND...: COMMAND again on a 403 until IAM has applied the grant.
retry_while_denied() {
  local what=$1 attempt answer
  shift
  for ((attempt = 1; attempt <= ACCESS_ATTEMPTS; attempt++)); do
    answer=$("$@" 2>&1) && return 0
    is_denied "$answer" || fail "$what : $(last_line "$answer")"
    if ((attempt < ACCESS_ATTEMPTS)); then
      echo "  $what : refus (403), nouvel essai dans $ACCESS_RETRY_SECONDS s (droits IAM)"
      sleep "$ACCESS_RETRY_SECONDS"
    fi
  done
  return 1
}

check_common_inputs() {
  require BUCKET "$BUCKET_FORMAT" \
    "le nom du bucket, 3 a 63 caracteres, minuscules, chiffres et tirets (lodb-pages-prod)"
  REGION=${REGION:-fr-par}
  require REGION "$REGION_FORMAT" "une region de Scaleway Object Storage (fr-par)"
  [[ -n "${AWS_ACCESS_KEY_ID:-}" && -n "${AWS_SECRET_ACCESS_KEY:-}" ]] ||
    fail "AWS_ACCESS_KEY_ID et AWS_SECRET_ACCESS_KEY : $1"
  command -v docker >/dev/null 2>&1 ||
    fail "Docker est requis : l'AWS CLI tourne dans son image officielle"
  endpoint="https://s3.$REGION.scw.cloud"
}

# The operator's key names the bucket's project after an @: Scaleway's override of the key's
# preferred project, so that the bucket is created there and nowhere else.
scope_operator_key() {
  if [[ "$AWS_ACCESS_KEY_ID" == *@* ]]; then
    [[ "${AWS_ACCESS_KEY_ID#*@}" == "$PROJECT_ID" ]] ||
      fail "AWS_ACCESS_KEY_ID nomme apres son @ un autre projet que PROJECT_ID"
  else
    AWS_ACCESS_KEY_ID="$AWS_ACCESS_KEY_ID@$PROJECT_ID"
  fi
  export AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY
}

read_policy() {
  local answer
  if answer=$(s3api get-bucket-policy --bucket "$BUCKET" 2>&1); then
    printf '%s' "$answer"
    return
  fi
  case $answer in
    *NoSuchBucketPolicy*) printf '{}' ;;
    *) fail "lecture de la politique : $(last_line "$answer")" ;;
  esac
}

# The way back from a policy that locks a principal out: as the Organization Owner, who
# keeps the right to manage policies whatever they say.
delete_policy() {
  local after
  check_common_inputs "$OPERATOR_KEY_HINT"
  require PROJECT_ID "$UUID_FORMAT" "l'identifiant du projet du bucket (UUID)"
  scope_operator_key
  printf 'Bucket %s (%s, projet %s)\n' "$BUCKET" "$endpoint" "$PROJECT_ID"
  s3api delete-bucket-policy --bucket "$BUCKET" >/dev/null
  # Read apart, so that a failed read stops the run with its own message.
  after=$(read_policy)
  [[ "$after" == '{}' ]] || fail "la politique est encore la apres sa suppression"
  echo "  politique supprimee : le bucket suit de nouveau les seuls droits IAM du projet."
  echo "Corriger l'entree fautive, puis relancer : bash $SCRIPT"
}

# shellcheck source=lib/configure.sh
source "$LIB_DIR/configure.sh"
# shellcheck source=lib/verify.sh
source "$LIB_DIR/verify.sh"

# Sourced (the bench does, to run one step alone), it only defines its functions.
[[ "${BASH_SOURCE[0]}" == "$0" ]] || return 0

case ${1:-configurer} in
  configurer) configure ;;
  verifier) verify ;;
  supprimer-politique) delete_policy ;;
  *) fail "commande inconnue : $1 (configurer, verifier ou supprimer-politique)" ;;
esac
