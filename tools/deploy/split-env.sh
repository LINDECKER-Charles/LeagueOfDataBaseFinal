#!/usr/bin/env bash
# Splits the dotenv of a served environment (secret ENV_STAGING or ENV_PROD), on the host:
# every secret setting goes to a file of its own, which compose.deploy.yaml mounts in
# /run/secrets of the services that read it, and every other line goes to the .env that
# Compose reads. A secret then never sits in a container's environment, which
# `docker inspect` shows to whoever can query the Docker API.
#
#   bash tools/deploy/split-env.sh ENV_FILE SECRETS_DIR < dotenv
#   bash tools/deploy/split-env.sh --keys      # the secret keys, one per line
#
# Every key of SECRET_KEYS gets its file, empty when the dotenv leaves it unset: Compose
# mounts every secret it declares, and the API reads an empty file as an unset setting.
# A value is read as Compose reads it, in the forms this script accepts: unquoted (an inline
# comment after a blank is dropped, surrounding blanks are trimmed), single-quoted (taken
# literally) or double-quoted without `\` nor `$`. Compose would interpolate a `$` outside
# single quotes: the script refuses it rather than guess, as it refuses a quote left open.
# Nothing is written before every value is read, and no message ever holds a value.
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
)
readonly REVISION_KEY=LODB_SECRETS_REVISION
readonly ASSIGNMENT='^[[:space:]]*(export[[:space:]]+)?([A-Za-z_][A-Za-z0-9_.-]*)[[:space:]]*=(.*)$'
readonly SECRETS_DIR_MODE=700
readonly SECRET_FILE_MODE=444

declare -A raw_values=()
declare -A values=()
kept_lines=()
parsed=''
secrets_changed=0

fail() {
  echo "split-env: $*" >&2
  exit 1
}

usage() {
  fail "usage: split-env.sh ENV_FILE SECRETS_DIR < dotenv | split-env.sh --keys"
}

is_secret() {
  local key
  for key in "${SECRET_KEYS[@]}"; do
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
      if is_secret "$key"; then
        raw_values["$key"]="${BASH_REMATCH[3]}"
        continue
      fi
      # Written by this script only, from the files it compares.
      [ "$key" != "$REVISION_KEY" ] || continue
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

# The quoted value at the start of $2 (opening quote $3), into `parsed`.
parse_quoted() {
  local key="$1" rest="${2:1}" quote="$3"
  [[ "$rest" == *"$quote"* ]] || fail "$key: the quote is not closed on its line."
  parsed="${rest%%"$quote"*}"
  check_tail "$key" "${rest#*"$quote"}"
}

# The unquoted value $2, into `parsed`: an inline comment starts at a blank followed by `#`.
parse_unquoted() {
  local value="${2%%[[:space:]]#*}"
  value="$(trim_leading "$value")"
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
  [ "$secrets_changed" = 0 ] || revision=$((revision + 1))
  write_env "$env_file" "$revision"
  echo "split-env: $filled of ${#SECRET_KEYS[@]} secrets set, revision $revision."
}

main "$@"
