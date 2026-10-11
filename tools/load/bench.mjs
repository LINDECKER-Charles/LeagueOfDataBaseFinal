// k6 entry of the load bench (README.md): one scenario (SCENARIO, S1 to S5) for DURATION, the
// human probe beside it. Run by run.sh in the official k6 image, on the stack's network.
//
//   k6 run -e SCENARIO=S1 -e DURATION=3m -e PROBE_BLOCK=1 -e OUT=/out/S1.k6.json bench.mjs
//
// BASE_URL is nginx on the stack's network, on its unprivileged port; every request names the
// site's host and the client it stands for (X-Forwarded-For, believed from the bench's network
// only). A bench stack has no edge secret: nginx asks for no X-LoDb-Edge.
import http from 'k6/http';
import exec from 'k6/execution';
import { Counter, Rate, Trend } from 'k6/metrics';
import { contextOf } from './lib/context.mjs';
import { addressAt, crowdOf, VISITOR_ADDRESS } from './lib/crowd.mjs';
import { optionsOf, PROBE, scenarioOf } from './lib/plan.mjs';
import { cacheClassOf, warmUpEndOf } from './lib/responses.mjs';
import { probePageAt, targetAt } from './lib/targets.mjs';

const SCENARIO = __ENV.SCENARIO;
const BASE_URL = __ENV.BASE_URL || 'http://nginx:8080';
const SITE_HOST = __ENV.SITE_HOST || 'localhost';
const PROBE_BLOCK = Number(__ENV.PROBE_BLOCK || 0);
const OUT = __ENV.OUT || '';
const DEFAULT_DURATION = '3m';
const READ_LANGUAGE = 'en_US';
const RESOURCES = ['champions', 'items', 'runes', 'summoners'];
const OK = 200;

export const options = optionsOf(SCENARIO, __ENV.DURATION || DEFAULT_DURATION);

const scenario = scenarioOf(SCENARIO);
const crowd = crowdOf(scenario.crowd);
const hitDuration = new Trend('probe_hit_duration', true);
const missDuration = new Trend('probe_miss_duration', true);
const probeFailed = new Rate('probe_failed');
const warmUpEnds = {
  done: new Counter('warm_up_done'),
  failed: new Counter('warm_up_failed'),
  cut: new Counter('warm_up_cut'),
};

function get(path, address, { timeoutSeconds, readsBody }) {
  return http.get(`${BASE_URL}${path}`, {
    headers: { Host: SITE_HOST, 'X-Forwarded-For': address },
    timeout: `${timeoutSeconds}s`,
    responseType: readsBody ? 'text' : 'none',
  });
}

function readJson(path) {
  const response = get(path, VISITOR_ADDRESS, { ...PROBE, readsBody: true });
  if (response.status !== OK) {
    throw new Error(`${path} answered ${response.status}: is the stack up and ingested?`);
  }
  return JSON.parse(response.body);
}

/** Reads the stack's versions, locales and latest entries once, for every request. */
export function setup() {
  const meta = readJson('/api/meta');
  const lists = Object.fromEntries(RESOURCES.map((resource) =>
    [resource, readJson(`/api/catalog/${meta.latest}/${READ_LANGUAGE}/${resource}`)]));
  return contextOf(meta, lists);
}

/** One request of the scenario, from the next client of its crowd. */
export function load(context) {
  const index = exec.scenario.iterationInTest;
  const response = get(targetAt(SCENARIO, context, index), addressAt(crowd, index),
    { ...scenario, readsBody: scenario.isStream });
  if (scenario.isStream) {
    warmUpEnds[warmUpEndOf(response.body).end].add(1);
  }
}

/** One page of the latest patch for the human, timed apart for a cached and a rendered one. */
export function probe(context) {
  const path = probePageAt(context, exec.scenario.iterationInTest, PROBE_BLOCK);
  const response = get(path, VISITOR_ADDRESS, PROBE);
  const isServed = response.status === OK;
  probeFailed.add(!isServed);
  if (!isServed) {
    return;
  }
  const cacheClass = cacheClassOf(response.headers['X-Cache-Status']);
  if (cacheClass === 'hit') {
    hitDuration.add(response.timings.duration);
  } else if (cacheClass === 'miss') {
    missDuration.add(response.timings.duration);
  }
}

function thresholdsMet(data) {
  return Object.values(data.metrics).every((metric) =>
    Object.values(metric.thresholds ?? {}).every((threshold) => threshold.ok));
}

/** The whole summary for summarize.mjs, and one line for the console. */
export function handleSummary(data) {
  const requests = data.metrics['http_reqs{traffic:load}']?.values.count ?? 0;
  const verdict = thresholdsMet(data) ? 'met' : 'missed';
  const line = `${SCENARIO}: ${requests} requests, probe thresholds ${verdict}\n`;
  return OUT ? { [OUT]: JSON.stringify(data), stdout: line } : { stdout: line };
}
