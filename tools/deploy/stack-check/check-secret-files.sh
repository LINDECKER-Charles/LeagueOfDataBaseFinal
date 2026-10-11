#!/usr/bin/env bash
# Proves on a local stack that the services read their secrets from files, as on a served
# host: split-env.sh splits a dotenv shaped like ENV_PROD, compose.secret-files.yaml mounts
# its files, and the environment holds wrong values the files must override. Checks:
#   - Postgres is created with the password of its file, which the API and migrate use;
#   - the Stripe webhook accepts an event signed with the secret of the file only;
#   - Mailpit, which accepts no other login, receives the mail the API sends with the
#     password of the file;
#   - no secret value is in the environment of api, migrate or postgres, and the API names
#     the settings it read from files.
#
# From the repository root, Docker running, on the slot lodb-dev-e1 (developpement.md) unless
# COMPOSE_PROJECT_NAME and the LODB_*_PORT variables name another; the stack is removed
# (`down -v`) at the end:
#   bash tools/deploy/stack-check/check-secret-files.sh
# The checks are functions that check() runs:
# shellcheck disable=SC2329
set -euo pipefail
shopt -s inherit_errexit

readonly DB_PASSWORD='check pass;word=1'
readonly MAIL_PASSWORD=mail-secret-from-the-file
readonly WEBHOOK_SECRET=whsec_secret_from_the_file
readonly WRONG_WEBHOOK_SECRET=whsec_wrong_secret_from_the_environment
readonly STRIPE_KEY=sk_test_secret_from_the_file
readonly VISITOR_KEY=visitor-key-from-the-file
readonly OVERLAY=tools/deploy/stack-check/compose.secret-files.yaml
readonly MAIL_WAIT_ATTEMPTS=45
readonly MAIL_WAIT_SECONDS=2

project="${COMPOSE_PROJECT_NAME:-lodb-dev-e1}"
export LODB_HTTP_PORT="${LODB_HTTP_PORT:-18180}" LODB_API_PORT="${LODB_API_PORT:-18181}"
export LODB_SSR_PORT="${LODB_SSR_PORT:-18182}" LODB_PG_PORT="${LODB_PG_PORT:-15532}"
export LODB_MAIL_PORT="${LODB_MAIL_PORT:-18125}"
api="http://localhost:$LODB_API_PORT"
mailpit="http://localhost:$LODB_MAIL_PORT"
work="$(mktemp -d)"
failed=0

compose() {
  docker compose -p "$project" -f compose.yaml -f compose.override.yaml -f "$OVERLAY" "$@"
}

check() {
  local name="$1"
  shift
  if "$@"; then
    echo "PASS $name"
  else
    echo "FAIL $name"
    failed=1
  fi
}

write_dotenv() {
  cat <<EOF
COMPOSE_PROJECT_NAME=$project
LODB_DB_PASSWORD='$DB_PASSWORD'
LoDb__Mail__Password=$MAIL_PASSWORD
LoDb__Billing__StripeSecretKey=$STRIPE_KEY
LoDb__Billing__StripeWebhookSecret="$WEBHOOK_SECRET"
LoDb__Analytics__VisitorKey=$VISITOR_KEY
EOF
}

# Status of an event posted to the webhook, signed with the secret $2.
post_event() {
  local created payload signature
  created="$(date +%s)"
  payload="{\"id\":\"evt_secret_files_$1\",\"object\":\"event\",\"created\":$created,"
  payload+="\"api_version\":\"2025-01-27.acacia\",\"livemode\":false,\"pending_webhooks\":1,"
  payload+="\"type\":\"balance.available\",\"data\":{\"object\":{\"object\":\"balance\"}}}"
  signature="$(printf '%s' "$created.$payload" | openssl dgst -sha256 -hmac "$2")"
  signature="${signature##*= }"
  curl -s -o /dev/null -w '%{http_code}' -H 'Content-Type: application/json' \
    -H "Stripe-Signature: t=$created,v1=$signature" --data "$payload" "$api/webhooks/stripe"
}

webhook_takes_the_file_secret() {
  [ "$(post_event file "$WEBHOOK_SECRET")" = 200 ] \
    && [ "$(post_event environment "$WRONG_WEBHOOK_SECRET")" = 400 ]
}

relay_takes_the_file_password() {
  local body sent attempt total
  body='{"category":"feedback","name":"Check","email":"visitor@example.test",'
  body+='"subject":"Secret files","message":"The relay takes the password of the file.",'
  body+='"locale":"en"}'
  sent="$(curl -s -o /dev/null -w '%{http_code}' -H 'Content-Type: application/json' \
    -H "Origin: $api" --data "$body" "$api/api/contact")"
  [ "$sent" = 204 ] || return 1
  for attempt in $(seq 1 "$MAIL_WAIT_ATTEMPTS"); do
    total="$(curl -s "$mailpit/api/v1/messages" | sed -n 's/.*"total":\([0-9]*\).*/\1/p')"
    [ "${total:-0}" -lt 1 ] || return 0
    [ "$attempt" = "$MAIL_WAIT_ATTEMPTS" ] || sleep "$MAIL_WAIT_SECONDS"
  done
  return 1
}

# Mailpit, a stand-in for the relay, holds the login it accepts: only the stack's own
# services are read. A failure names the service and the setting, never the value.
no_secret_in_any_environment() {
  local service environment index
  local -a names=(LODB_DB_PASSWORD Mail__Password StripeWebhookSecret StripeSecretKey VisitorKey)
  local -a secrets=("$DB_PASSWORD" "$MAIL_PASSWORD" "$WEBHOOK_SECRET" "$STRIPE_KEY" "$VISITOR_KEY")
  for service in api migrate postgres; do
    environment="$(compose ps -aq "$service" | xargs docker inspect \
      --format '{{range .Config.Env}}{{println .}}{{end}}')"
    for index in "${!secrets[@]}"; do
      if grep -qF -- "${secrets[$index]}" <<<"$environment"; then
        echo "  $service holds ${names[$index]} in its environment" >&2
        return 1
      fi
    done
  done
}

api_names_its_secret_files() {
  local line
  line="$(compose logs --no-color api | grep -F 'hosting.secret_files.loaded' | tail -n 1)"
  [[ "$line" == *LoDb:Billing:StripeWebhookSecret* && "$line" == *LoDb:Database:Password* ]] \
    && [[ "$line" != *"$WEBHOOK_SECRET"* && "$line" != *"$MAIL_PASSWORD"* ]]
}

secrets_dir="$work/secrets"
# Docker Desktop on Windows mounts Windows paths only.
if command -v cygpath > /dev/null; then
  secrets_dir="$(cygpath -m "$secrets_dir")"
fi
write_dotenv | bash tools/deploy/split-env.sh "$work/.env" "$secrets_dir"
export LODB_SECRET_FILES_DIR="$secrets_dir"
export LODB_CHECK_SMTP_AUTH="lodb:$MAIL_PASSWORD"
trap 'compose down -v --remove-orphans > /dev/null 2>&1 || true; rm -rf "$work"' EXIT
compose up -d --build --wait --wait-timeout 300 api mailpit

check "postgres, migrate and the API use the database password of the file" \
  curl -fsS -o /dev/null "$api/readyz"
check "the Stripe webhook takes the secret of the file over the environment's" \
  webhook_takes_the_file_secret
check "the mail relay takes the password of the file over the environment's" \
  relay_takes_the_file_password
check "no secret value in the environment of api, migrate or postgres" \
  no_secret_in_any_environment
check "the API names the settings it read from files, without their values" \
  api_names_its_secret_files
exit "$failed"
