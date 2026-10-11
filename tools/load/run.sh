#!/usr/bin/env bash
# k6 load bench of the stack (tools/load/README.md), from the repository root:
#
#   bash tools/load/run.sh
#
# Builds and starts a slot (lodb-dev-e1 by default) with tools/load/compose.bench.yaml over
# compose.override.yaml, waits until the latest patch is promoted, then runs each scenario: a
# warm-up, then the measured run while `docker stats` samples web-ssr, api, postgres and nginx
# every 5 s, then the containers' health (OOM kills, restarts, killed processes). Writes
# tools/load/.runs/<UTC time>/ (summary.md, summary.json and the raw files), then removes the
# slot (`down -v`). Exit code: 0 every criterion met, 1 one missed, 2 the bench did not run.
#
# Settings, from the environment:
#   LODB_BENCH_PROJECT    slot project, lodb-dev-e<n> only (lodb-dev-e1)
#   LODB_HTTP_PORT…       the slot's ports, as compose.override.yaml reads them (slot 1's)
#   LODB_BENCH_SCENARIOS  scenarios to run, in order ("S1 S2 S3 S4 S5")
#   LODB_BENCH_DURATION   measured run of each scenario, a k6 duration (3m)
#   LODB_BENCH_WARMUP     unmeasured run before it, same load (30s)
#   LODB_BENCH_COOLDOWN   seconds of rest between two scenarios (30)
#   LODB_BENCH_READY_WAIT seconds allowed for the latest patch's ingestion (1800)
#   LODB_BENCH_SETTLE     seconds left to the API to pin the latest catalogs (60)
#   LODB_BENCH_OUT        run folder (tools/load/.runs/<UTC time>)
#   LODB_BENCH_KEEP_STACK 1 keeps the slot running at the end
#   LODB_BENCH_WRAP       command prefix of the build and of each scenario (`nice -n 10`…)
set -euo pipefail

readonly SELF="${BASH_SOURCE[0]}"
ROOT="$(cd "$(dirname "$SELF")/../.." && pwd)"
readonly ROOT
# Official image, pinned by version and digest (index of every platform).
readonly K6_DIGEST='sha256:9c2dee7f8ed74d317e4027c06a10f169b625638189de8d4555d0b3486a5aeb34'
readonly K6_IMAGE="grafana/k6:2.3.0@$K6_DIGEST"
readonly SERVICES=(web-ssr api postgres nginx)
readonly SAMPLE_PERIOD=5
readonly READY_POLL=10
readonly UP_TIMEOUT=900
# Lines of a container's log that tell of a process killed for its memory or by SIGKILL:
# postgres's backends, nginx's workers, V8's heap, .NET's.
readonly KILLED='signal 9|out of memory|OutOfMemory'
# Prints the cpu.stat of each "<service>=<container id>" from the Docker host's cgroup tree
# (cgroup v2, cgroupfs or systemd driver), each after a "@ <service>" line.
readonly CGROUP_READER='for pair; do s=${pair%%=*}; id=${pair#*=}
for d in /sys/fs/cgroup/docker/$id /sys/fs/cgroup/system.slice/docker-$id.scope; do
if [ -f "$d/cpu.stat" ]; then echo "@ $s"; cat "$d/cpu.stat"; fi; done; done'

export LODB_BENCH_PROJECT="${LODB_BENCH_PROJECT:-lodb-dev-e1}"
export LODB_HTTP_PORT="${LODB_HTTP_PORT:-18180}" LODB_API_PORT="${LODB_API_PORT:-18181}"
export LODB_SSR_PORT="${LODB_SSR_PORT:-18182}" LODB_PG_PORT="${LODB_PG_PORT:-15532}"
export LODB_MAIL_PORT="${LODB_MAIL_PORT:-18125}"
export LODB_BENCH_SCENARIOS="${LODB_BENCH_SCENARIOS:-S1 S2 S3 S4 S5}"
export LODB_BENCH_DURATION="${LODB_BENCH_DURATION:-3m}"
export LODB_BENCH_WARMUP="${LODB_BENCH_WARMUP:-30s}"
export LODB_BENCH_OUT="${LODB_BENCH_OUT:-$ROOT/tools/load/.runs/$(date -u +%Y%m%dT%H%M%SZ)}"
readonly COOLDOWN="${LODB_BENCH_COOLDOWN:-30}"
readonly READY_WAIT="${LODB_BENCH_READY_WAIT:-1800}"
readonly SETTLE="${LODB_BENCH_SETTLE:-60}"
read -r -a WRAP <<<"${LODB_BENCH_WRAP:-}"
readonly PROJECT="$LODB_BENCH_PROJECT" OUT="$LODB_BENCH_OUT"
readonly COMPOSE=(docker compose -p "$PROJECT" -f compose.yaml -f compose.override.yaml
  -f tools/load/compose.bench.yaml)

# A path the Docker CLI reads as the host's (Git Bash hands it /c/… otherwise).
host_path() {
  if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi
}

container_of() {
  "${COMPOSE[@]}" ps -q "$1"
}

# One run of bench.mjs on the slot's network: <scenario> <warmup|measured> <ordinal>. Each
# phase of each scenario walks its own block of the probe's cold pages.
run_k6() {
  local duration=$LODB_BENCH_WARMUP block=$((2 * $3)) out="$1.warmup.k6.json"
  if [[ $2 == measured ]]; then
    duration=$LODB_BENCH_DURATION block=$((2 * $3 + 1)) out="$1.k6.json"
  fi
  MSYS_NO_PATHCONV=1 docker run --rm --name "$PROJECT-k6" --network "${PROJECT}_default" \
    --user "$(id -u):$(id -g)" \
    -v "$(host_path "$ROOT/tools/load"):/bench:ro" -v "$(host_path "$OUT"):/out" \
    "$K6_IMAGE" run --quiet -e SCENARIO="$1" -e DURATION="$duration" \
    -e PROBE_BLOCK="$block" -e OUT="/out/$out" /bench/bench.mjs
}

sample_stats() {
  local ids=() service rest
  for service in "${SERVICES[@]}"; do ids+=("$(container_of "$service")"); done
  while :; do
    rest=$(($(date +%s) + SAMPLE_PERIOD))
    docker stats --no-stream --format '{{json .}}' "${ids[@]}" >>"$1" 2>/dev/null || true
    rest=$((rest - $(date +%s)))
    if ((rest > 0)); then sleep "$rest"; fi
  done
}

now_ms() {
  local now
  now=$(date +%s%3N)
  [[ $now =~ ^[0-9]+$ ]] || now=$(($(date +%s) * 1000))
  printf '%s' "$now"
}

# The CPU counters of each container (time used, periods, throttled periods) after a
# "# <label> <epoch ms>" line, read by a throwaway container of the k6 image: their
# difference over a run gives its exact mean and how often the quota throttled it.
cpu_counters() {
  local pairs=() service
  for service in "${SERVICES[@]}"; do pairs+=("$service=$(container_of "$service")"); done
  printf '# %s %s\n' "$1" "$(now_ms)"
  MSYS_NO_PATHCONV=1 docker run --rm --entrypoint sh --cgroupns=host \
    -v /sys/fs/cgroup:/sys/fs/cgroup:ro "$K6_IMAGE" -c "$CGROUP_READER" sh "${pairs[@]}" \
    || true
}

# "<service> <restarts> <oomKilled>" of each container, before a measured run.
states_of() {
  local service
  for service in "${SERVICES[@]}"; do
    printf '%s %s\n' "$service" "$(docker inspect --format \
      '{{.RestartCount}} {{.State.OOMKilled}}' "$(container_of "$service")")"
  done
}

# One JSON line per container: killed for its memory, restarted, or one of its processes
# killed since <since>, against the states before the run.
health_of() {
  local since=$1 before=$2 service restarts oom id now_restarts now_oom kills
  while read -r service restarts oom; do
    id=$(container_of "$service")
    read -r now_restarts now_oom <<<"$(docker inspect --format \
      '{{.RestartCount}} {{.State.OOMKilled}}' "$id")"
    kills=$(docker logs --since "$since" "$id" 2>&1 | grep -ciE "$KILLED" || true)
    # A flag already up before the run tells of an earlier kill: the restarts tell this one.
    if [[ $oom == true ]]; then now_oom=false; fi
    printf '{"service":"%s","oomKilled":%s,"restarts":%d,"kills":%d}\n' \
      "$service" "$now_oom" "$((now_restarts - restarts))" "$kills"
  done <<<"$before"
}

# The logs that explain a measured run's figures: the SSR's admission summaries (one a
# minute) and the API's warnings and errors, counted by event.
logs_of() {
  local since=$1 name=$2
  docker logs --since "$since" "$(container_of web-ssr)" 2>&1 \
    | grep '"msg":"ssr.admission.summary"' >"$OUT/$name.admission.ndjson" || true
  docker logs --since "$since" "$(container_of api)" 2>&1 \
    | grep -E '"LogLevel":"(Warning|Error|Critical)"' | grep -oE '"EventName":"[^"]*"' \
    | sort | uniq -c | sort -rn >"$OUT/$name.api-events.txt" || true
}

# One scenario: warm-up, then the measured run under sampling, then the containers' health.
run_scenario() {
  local name=$1 ordinal=$2 since before status=0
  echo "bench: $name warm-up ($LODB_BENCH_WARMUP)" >&2
  run_k6 "$name" warmup "$ordinal" || true
  before=$(states_of)
  since=$(date -u +%Y-%m-%dT%H:%M:%SZ)
  sample_stats "$OUT/$name.stats.ndjson" &
  SAMPLER=$!
  cpu_counters before >"$OUT/$name.cpu.txt"
  echo "bench: $name measured ($LODB_BENCH_DURATION)" >&2
  run_k6 "$name" measured "$ordinal" || status=$?
  cpu_counters after >>"$OUT/$name.cpu.txt"
  kill "$SAMPLER" 2>/dev/null || true
  wait "$SAMPLER" 2>/dev/null || true
  # 99: thresholds missed, judged by summarize.mjs with the rest.
  [[ $status == 0 || $status == 99 ]] || echo "bench: k6 ended with $status on $name" >&2
  health_of "$since" "$before" >"$OUT/$name.health.ndjson"
  logs_of "$since" "$name"
}

wait_latest() {
  local deadline=$(($(date +%s) + READY_WAIT)) meta
  while (($(date +%s) < deadline)); do
    meta=$(curl -fsS "http://localhost:$LODB_HTTP_PORT/api/meta" 2>/dev/null || true)
    if [[ $meta =~ \"latest\":\"([^\"]+)\" ]]; then
      printf '%s' "${BASH_REMATCH[1]}"
      return 0
    fi
    sleep "$READY_POLL"
  done
  return 1
}

write_run() {
  local scenarios
  scenarios=$(printf '"%s",' $LODB_BENCH_SCENARIOS)
  printf '{"date":"%s","revision":"%s","project":"%s","latest":"%s","duration":"%s",' \
    "$(date -u +%Y-%m-%dT%H:%MZ)" "$(git rev-parse --short HEAD 2>/dev/null || echo unknown)" \
    "$PROJECT" "$1" "$LODB_BENCH_DURATION" >"$OUT/run.json"
  printf '"warmUp":"%s","k6Image":"%s","docker":{"cpus":%s,"memoryBytes":%s},' \
    "$LODB_BENCH_WARMUP" "$K6_IMAGE" "$(docker info --format '{{.NCPU}}')" \
    "$(docker info --format '{{.MemTotal}}')" >>"$OUT/run.json"
  printf '"scenarios":[%s]}\n' "${scenarios%,}" >>"$OUT/run.json"
}

teardown() {
  docker rm -f "$PROJECT-k6" >/dev/null 2>&1 || true
  if [[ ${LODB_BENCH_KEEP_STACK:-0} != 1 ]]; then
    "${COMPOSE[@]}" down -v --remove-orphans >/dev/null 2>&1 || true
  fi
}

main() {
  local latest ordinal=0 name
  mkdir -p "$OUT"
  trap teardown EXIT
  "${WRAP[@]}" "${COMPOSE[@]}" up -d --build --wait --wait-timeout "$UP_TIMEOUT" \
    || { echo "bench: the stack did not start" >&2; exit 2; }
  latest=$(wait_latest) || { echo "bench: no patch promoted within $READY_WAIT s" >&2; exit 2; }
  echo "bench: latest patch $latest; settling ${SETTLE}s" >&2
  sleep "$SETTLE"
  write_run "$latest"
  for name in $LODB_BENCH_SCENARIOS; do
    if ((ordinal > 0)); then sleep "$COOLDOWN"; fi
    "${WRAP[@]}" bash "$SELF" scenario "$name" "$ordinal" \
      || echo "bench: $name did not complete" >&2
    ordinal=$((ordinal + 1))
  done
  node tools/load/summarize.mjs "$OUT"
}

if [[ ! $PROJECT =~ ^lodb-dev-e[0-9]+$ ]]; then
  echo "bench: $PROJECT is not a slot (lodb-dev-e<n>); the bench removes its stack" >&2
  exit 2
fi
cd "$ROOT"
if [[ ${1:-} == scenario ]]; then
  SAMPLER=''
  trap '[[ -z $SAMPLER ]] || kill "$SAMPLER" 2>/dev/null || true' EXIT
  run_scenario "$2" "$3"
else
  main
fi
