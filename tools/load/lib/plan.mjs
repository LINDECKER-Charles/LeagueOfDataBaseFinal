// The bench's scenarios as k6 options: requests at a constant arrival rate from a crowd of
// clients, the human probe beside them, and the thresholds the verdict reads.
// Plain module: imported by the k6 scripts and by node --test alike.

// The crawl of 2026-10-10: 237 cloud addresses, 23 requests a second in all.
const INCIDENT_CROWD = 237;
const INCIDENT_RATE = 23;
const SEARCH_RATE = 20;
const API_SWEEP_RATE = 10;
const WARM_UP_RATE = 2;
const REQUEST_TIMEOUT_SECONDS = 30;
// A warm-up stream lasts 120 s at most (LoDb:Catalog:WarmUp:StreamLifetime).
const STREAM_TIMEOUT_SECONDS = 150;

/** Rate (requests a second), crowd (client addresses) and timeout of each scenario. */
const SCENARIOS = {
  S1: { rate: INCIDENT_RATE, crowd: INCIDENT_CROWD, timeoutSeconds: REQUEST_TIMEOUT_SECONDS },
  S2: { rate: SEARCH_RATE, crowd: 1, timeoutSeconds: REQUEST_TIMEOUT_SECONDS },
  S3: { rate: API_SWEEP_RATE, crowd: INCIDENT_CROWD, timeoutSeconds: REQUEST_TIMEOUT_SECONDS },
  S4: { rate: INCIDENT_RATE, crowd: INCIDENT_CROWD, timeoutSeconds: REQUEST_TIMEOUT_SECONDS },
  S5: {
    rate: WARM_UP_RATE,
    crowd: INCIDENT_CROWD,
    timeoutSeconds: STREAM_TIMEOUT_SECONDS,
    isStream: true,
  },
};

/** The human probe: one page a second from one address, beside every scenario. */
export const PROBE = { rate: 1, crowd: 1, timeoutSeconds: REQUEST_TIMEOUT_SECONDS };

// A desktop browser, as the crawl of 2026-10-10 posed as one (not its exact build, which
// nginx refuses outright).
const BROWSER_AGENT =
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) ' +
  'Chrome/140.0.0.0 Safari/537.36';

// Statuses counted apart in the summary; any other is counted as "other".
const STATUSES = [0, 200, 301, 302, 304, 400, 403, 404, 413, 426, 429, 500, 502, 503, 504];
const TRAFFIC = ['load', 'probe'];
const HIT_P95_MS = 100;
const MISS_P95_MS = 1500;
const PROBE_FAILED_RATE = 0.01;
// Virtual users: enough for a second of answers at the start, at most what the generator
// holds comfortably; a run short of them counts the requests it dropped.
const ANSWER_SECONDS = 2;
const MIN_VUS = 4;
const MAX_VUS = 300;
const SETUP_TIMEOUT = '120s';
// Each scenario measures the answer to the URL it models: a 301 of the former site is that
// answer, the page it leads to is S1's.
const NO_REDIRECTS = 0;
const TREND_STATS = ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)', 'count'];

/** The definition of a scenario, S1 to S5. */
export function scenarioOf(name) {
  const scenario = SCENARIOS[name];
  if (scenario === undefined) {
    throw new RangeError(`Unknown scenario ${name}: ${Object.keys(SCENARIOS).join(', ')}.`);
  }
  return scenario;
}

function arrivalOf(traffic, { rate, timeoutSeconds }, duration) {
  const maxVUs = Math.max(MIN_VUS, Math.min(MAX_VUS, rate * timeoutSeconds));
  return {
    executor: 'constant-arrival-rate',
    exec: traffic,
    rate,
    timeUnit: '1s',
    duration,
    preAllocatedVUs: Math.min(maxVUs, Math.max(MIN_VUS, rate * ANSWER_SECONDS)),
    maxVUs,
    tags: { traffic },
  };
}

/**
 * The verdict's thresholds: the probe's latency on cached and rendered pages and its share
 * of failures. The status counts never fail; declared, they make k6 count each status apart.
 */
export function thresholdsOf() {
  const counts = TRAFFIC.flatMap((traffic) => [
    [`http_reqs{traffic:${traffic}}`, ['count>=0']],
    ...STATUSES.map((status) => [`http_reqs{traffic:${traffic},status:${status}}`, ['count>=0']]),
  ]);
  return {
    probe_hit_duration: [`p(95)<${HIT_P95_MS}`],
    probe_miss_duration: [`p(95)<${MISS_P95_MS}`],
    probe_failed: [`rate<${PROBE_FAILED_RATE}`],
    ...Object.fromEntries(counts),
  };
}

/** The k6 options of a scenario run for `duration` ("3m"), its probe beside it. */
export function optionsOf(name, duration) {
  return {
    discardResponseBodies: true,
    maxRedirects: NO_REDIRECTS,
    userAgent: BROWSER_AGENT,
    setupTimeout: SETUP_TIMEOUT,
    summaryTrendStats: TREND_STATS,
    scenarios: {
      load: arrivalOf('load', scenarioOf(name), duration),
      probe: arrivalOf('probe', PROBE, duration),
    },
    thresholds: thresholdsOf(),
  };
}
