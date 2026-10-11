#!/usr/bin/env bash
# Splits the dotenv of a served environment (secret ENV_STAGING or ENV_PROD), on the host:
# every secret setting goes to a file of its own, which compose.deploy.yaml mounts in
# /run/secrets of the services that read it, and every other line goes to the .env that
# Compose reads. A secret then never sits in a container's environment, which
# `docker inspect` shows to whoever can query the Docker API.
#
#   bash tools/deploy/split-env.sh ENV_FILE SECRETS_DIR < dotenv
#   bash tools/deploy/split-env.sh --keys        # the secret keys, one per line
#   bash tools/deploy/split-env.sh --generated   # the secrets generated on the host
#
# Every key of SECRET_KEYS gets its file, empty when the dotenv leaves it unset: Compose
# mounts every secret it declares, and the API reads an empty file as an unset setting.
# A value is read as Compose reads it, in the forms this script accepts: unquoted (leading
# blanks dropped, then an inline comment from a space followed by `#`, then trailing blanks),
# single-quoted (taken literally) or double-quoted, without `\` before the closing quote nor
# `\` and `$` inside double quotes. Compose would interpolate a `$` outside single quotes and
# reads `\'` as an escaped quote: the script refuses those rather than guess, as it refuses a
# quote left open and a secret written `KEY: value`, a form Compose also reads.
# Nothing is written before every value is read, and no message ever holds a value.
#
# The keys of GENERATED_KEYS never come from the dotenv, which is refused when it sets one:
# the first run draws each one at random (openssl rand) into its file, and every later run
# keeps that file. No copy exists outside the host, GitHub's secrets included. Deleting the
# file draws a new value at the next run, which `migrate` then sets (configuration.md).
#
# The files are world-readable in a folder that only root can enter: Compose bind-mounts a
# secret file with its owner and mode, and the API runs as a non-root user.
#
# LODB_SECRETS_REVISION, appended to ENV_FILE, goes up by one whenever a secret file
# changes. compose.deploy.yaml labels the API with it, so that `up` recreates the API, which
# reads its files at start-up only.
set -euo pipefail
shopt -s inherit_errexit

readonly SECRET_KEYS=(
  LODB_DB_PASSWORD
  LoDb__DataProtection__CertificatePassword
  LoDb__Mail__Password
  LoDb__Billing__StripeSecretKey
  LoDb__Billing__StripeWebhookSecret
  LoDb__Accounts__Google__ClientSecret
  LoDb__Accounts__Google__AppClients__0__ClientSecret
  LoDb__Accounts__Google__AppClients__1__ClientSecret
  LoDb__Analytics__VisitorKey
  LoDb__Audit__IdentifierKey
  LoDb__Audience__Username
  LoDb__Audience__Password
)
# The password of the API's own database role (lodb_app).
readonly GENERATED_KEYS=(
  LODB_APP_DB_PASSWORD
)
readonly GENERATED_BYTES=32
readonly REVISION_KEY=LODB_SECRETS_REVISION
readonly KEY_PREFIX='^[[:space:]]*(export[[:space:]]+)?([A-Za-z_][A-Za-z0-9_.-]*)[[:space:]]*'
readonly ASSIGNMENT="${KEY_PREFIX}=(.*)\$"
readonly COLON_ASSIGNMENT="${KEY_PREFIX}:"
# Compose ends an unquoted value at a space followed by `#`, not at a tab.
readonly INLINE_COMMENT=' #'
readonly SECRETS_DIR_MODE=700
readonly SECRET_FILE_MODE=444

declare -A raw_values=()
declare -A values=()
kept_lines=()
parsed=''
secrets_changed=0
generated=0

fail() {
  echo "split-env: $*" >&2
  exit 1
}

usage() {
  fail "usage: split-env.sh ENV_FILE SECRETS_DIR < dotenv | split-env.sh --keys|--generated"
}

is_secret() {
  local key
  for key in "${SECRET_KEYS[@]}"; do
    [ "$key" != "$1" ] || return 0
  done
  return 1
}

is_generated() {
  local key
  for key in "${GENERATED_KEYS[@]}"; do
    [ "$key" != "$1" ] || return 0
  done
  return 1
}

# The dotenv from the input: the raw value of each secret (the last assignment wins, as in
# Compose), and every other line as it came, without its carriage return.
read_dotenv() {
  local line key
  while IFS= read -r line || [ -n "$line" ]; do
    line="${line%$'\r'}"
    if [[ "$line" =~ $ASSIGNMENT ]]; then
      key="${BASH_REMATCH[2]}"
      ! is_generated "$key" || fail "$key is generated on the host: remove it from the dotenv."
      if is_secret "$key"; then
        raw_values["$key"]="${BASH_REMATCH[3]}"
        continue
      fi
      # Written by this script only, from the files it compares.
      [ "$key" != "$REVISION_KEY" ] || continue
    elif [[ "$line" =~ $COLON_ASSIGNMENT ]]; then
      key="${BASH_REMATCH[2]}"
      ! is_generated "$key" || fail "$key is generated on the host: remove it from the dotenv."
      ! is_secret "$key" || fail "$key: write the secret as KEY=value."
    fi
    kept_lines+=("$line")
  done
}

trim_leading() {
  printf '%s' "${1#"${1%%[![:space:]]*}"}"
}

# After a closing quote, only blanks and a comment may follow.
check_tail() {
  local tail
  tail="$(trim_leading "$2")"
  [ -z "$tail" ] || [[ "$tail" == \#* ]] || fail "$1: unexpected characters after the quote."
}

# The quoted value at the start of $2 (opening quote $3), into `parsed`. Compose skips a
# quote preceded by `\`: the quote found here would not close the value there.
parse_quoted() {
  local key="$1" rest="${2:1}" quote="$3"
  [[ "$rest" == *"$quote"* ]] || fail "$key: the quote is not closed on its line."
  parsed="${rest%%"$quote"*}"
  [[ "$parsed" != *\\ ]] || fail "$key: \\ before the closing quote; remove the \\."
  check_tail "$key" "${rest#*"$quote"}"
}

# The unquoted value $2, into `parsed`, cut as Compose cuts it: leading blanks, then the
# inline comment, then trailing blanks.
parse_unquoted() {
  local value
  value="$(trim_leading "$2")"
  value="${value%%"$INLINE_COMMENT"*}"
  parsed="${value%"${value##*[![:space:]]}"}"
  [[ "$parsed" != *'$'* ]] || fail "$1: \$ outside single quotes; write the value as '...'."
}

parse_value() {
  local key="$1" raw="$2" start
  start="$(trim_leading "$raw")"
  case "$start" in
    \'*) parse_quoted "$key" "$start" "'" ;;
    \"*)
      parse_quoted "$key" "$start" '"'
      if [[ "$parsed" == *"\\"* || "$parsed" == *'$'* ]]; then
        fail "$key: \\ or \$ inside double quotes; write the value as '...'."
      fi
      ;;
    *) parse_unquoted "$key" "$raw" ;;
  esac
  values["$key"]="$parsed"
}

# Writes one file per secret, each replaced in one rename; notes in `secrets_changed`
# whether any content changed.
write_secrets() {
  local dir="$1" key file staged
  mkdir -p "$dir"
  chmod "$SECRETS_DIR_MODE" "$dir"
  for key in "${SECRET_KEYS[@]}"; do
    file="$dir/$key"
    staged="$(mktemp "$dir/.$key.XXXXXX")"
    printf '%s' "${values[$key]}" > "$staged"
    chmod "$SECRET_FILE_MODE" "$staged"
    cmp -s "$staged" "$file" || secrets_changed=1
    mv -f "$staged" "$file"
  done
}

# Draws each generated secret that has no file yet, replaced in one rename like the others;
# notes it in `secrets_changed` and counts it in `generated`.
write_generated() {
  local dir="$1" key file staged
  for key in "${GENERATED_KEYS[@]}"; do
    file="$dir/$key"
    [ ! -s "$file" ] || continue
    command -v openssl > /dev/null || fail "openssl is required to generate $key."
    staged="$(mktemp "$dir/.$key.XXXXXX")"
    # Hex digits only: no line end, whichever openssl build prints it.
    openssl rand -hex "$GENERATED_BYTES" | tr -dc '0-9a-f' > "$staged"
    [ -s "$staged" ] || fail "openssl drew no value for $key."
    chmod "$SECRET_FILE_MODE" "$staged"
    mv -f "$staged" "$file"
    secrets_changed=1
    generated=$((generated + 1))
  done
}

# The revision the previous run left in ENV_FILE, 0 without one.
previous_revision() {
  local revision=''
  if [ -f "$1" ]; then
    revision="$(sed -n "s/^$REVISION_KEY=//p" "$1" | tail -n 1)"
  fi
  [[ "$revision" =~ ^[0-9]+$ ]] || revision=0
  echo "$revision"
}

write_env() {
  local file="$1" revision="$2" staged
  staged="$(mktemp "$(dirname "$file")/.env.XXXXXX")"
  {
    [ "${#kept_lines[@]}" -eq 0 ] || printf '%s\n' "${kept_lines[@]}"
    printf '%s=%s\n' "$REVISION_KEY" "$revision"
  } > "$staged"
  mv -f "$staged" "$file"
}

main() {
  if [ "$#" -eq 1 ] && [ "$1" = --keys ]; then
    printf '%s\n' "${SECRET_KEYS[@]}"
    return
  fi
  if [ "$#" -eq 1 ] && [ "$1" = --generated ]; then
    printf '%s\n' "${GENERATED_KEYS[@]}"
    return
  fi
  [ "$#" -eq 2 ] || usage
  local env_file="$1" secrets_dir="$2" key revision filled=0
  umask 077
  read_dotenv
  for key in "${SECRET_KEYS[@]}"; do
    parse_value "$key" "${raw_values[$key]-}"
    [ -z "${values[$key]}" ] || filled=$((filled + 1))
  done
  revision="$(previous_revision "$env_file")"
  write_secrets "$secrets_dir"
  write_generated "$secrets_dir"
  [ "$secrets_changed" = 0 ] || revision=$((revision + 1))
  write_env "$env_file" "$revision"
  echo "split-env: $filled of ${#SECRET_KEYS[@]} secrets set, $generated generated," \
    "revision $revision."
}

main "$@"
