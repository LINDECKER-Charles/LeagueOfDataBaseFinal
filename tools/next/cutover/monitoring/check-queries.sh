#!/usr/bin/env bash
# Runs the monitoring queries (queries.logsql) against a throwaway VictoriaLogs fed with the
# logs of a local stack, as Vector would feed the host's: each container line becomes _msg,
# with the stack and service stream fields. Proves every query parses and counts on the real
# JSON lines of the new stack; the edge queries find no line here (no edge journal locally).
#
#   tools/next/cutover/monitoring/check-queries.sh [project] [domain]
#
# project  the Compose project whose logs are loaded, lodb-next by default (a slot:
#          lodb-next-e2). Its containers must exist; stopped ones keep their logs.
# domain   the value of {{domain}}, league-of-data-base.com by default.
#
# Prints each query's name, then its result rows (or its error). Exit code 1 when a query is
# refused, 0 otherwise. The container is removed at the end.
set -euo pipefail

readonly PROJECT="${1:-lodb-next}"
readonly DOMAIN="${2:-league-of-data-base.com}"
readonly WINDOW=24h
readonly IMAGE=victoriametrics/victoria-logs:v1.52.0
readonly QUERIES="$(cd "$(dirname "$0")" && pwd)/queries.logsql"

container="lodb-cutover-vlogs-$$"
work="$(mktemp -d)"
cleanup() {
  docker rm -f "$container" >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

docker run -d --name "$container" -p 127.0.0.1::9428 "$IMAGE" >/dev/null
port="$(docker port "$container" 9428/tcp | head -n 1 | sed 's/.*://')"
readonly VLOGS="http://127.0.0.1:$port"
for _ in $(seq 1 30); do
  if curl -fsS "$VLOGS/health" >/dev/null 2>&1; then break; fi
  sleep 1
done

# ── Load the stack's logs, one stream per service ────────────────────────────────────
services="$(docker ps -a --filter "label=com.docker.compose.project=$PROJECT" \
  --format '{{ .Label "com.docker.compose.service" }}' | sort -u)"
if [ -z "$services" ]; then
  echo "No container of the project $PROJECT." >&2
  exit 2
fi
for service in $services; do
  docker ps -aq --filter "label=com.docker.compose.project=$PROJECT" \
    --filter "label=com.docker.compose.service=$service" |
    while read -r id; do docker logs "$id" 2>&1; done |
    jq -Rc --arg stack "$PROJECT" --arg service "$service" \
      '{_msg: ., stack: $stack, service: $service}' >"$work/$service.jsonl"
  # The content type matters: as a form, the body is accepted and silently dropped.
  curl -fsS -X POST -H 'Content-Type: application/stream+json' \
    --data-binary "@$work/$service.jsonl" \
    "$VLOGS/insert/jsonline?_stream_fields=stack,service" >/dev/null
  echo "loaded $(wc -l <"$work/$service.jsonl" | tr -d ' ') lines of $service"
done
curl -fsS "$VLOGS/internal/force_flush" >/dev/null 2>&1 || true
sleep 2

# ── Run every query ──────────────────────────────────────────────────────────────────
refused=0
run() {
  local name="$1" query="$2" status
  query="${query//\{\{stack\}\}/$PROJECT}"
  query="${query//\{\{domain\}\}/$DOMAIN}"
  query="${query//\{\{window\}\}/$WINDOW}"
  status="$(curl -sS -o "$work/result" -w '%{http_code}' "$VLOGS/select/logsql/query" \
    --data-urlencode "query=$query")"
  if [ "$status" = 200 ]; then
    echo "ok   $name: $(grep -c . "$work/result" || true) row(s)"
    sed 's/^/       /' "$work/result" | head -n 10
  else
    echo "FAIL $name: $status $(head -c 300 "$work/result")"
    refused=$((refused + 1))
  fi
}

name=""
query=""
while IFS= read -r line || [ -n "$line" ]; do
  if [[ "$line" =~ ^#\ name:\ ([a-z0-9-]+) ]]; then
    name="${BASH_REMATCH[1]}"
    query=""
  elif [ -n "$name" ] && [ -n "$line" ] && [[ ! "$line" =~ ^# ]]; then
    query="$query $line"
  elif [ -n "$name" ] && [ -z "$line" ] && [ -n "$query" ]; then
    run "$name" "$query"
    name=""
  fi
done <"$QUERIES"
if [ -n "$name" ] && [ -n "$query" ]; then
  run "$name" "$query"
fi

if [ "$refused" -gt 0 ]; then
  echo "$refused query(ies) refused by VictoriaLogs."
  exit 1
fi
echo "Every query parses and runs."
