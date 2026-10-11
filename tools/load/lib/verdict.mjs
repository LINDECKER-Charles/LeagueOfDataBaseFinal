// The criteria of the bench and their verdict on one scenario's figures: what a crawl may
// cost the host, and what the human still gets meanwhile.

/** The bounds a scenario must hold, every one of them. */
export const CRITERIA = {
  ssrCores: 0.6,
  apiCores: 0.6,
  hitP95Ms: 100,
  missP95Ms: 1500,
  probeFailedRate: 0.01,
};

const WATCHED = ['web-ssr', 'api', 'postgres', 'nginx'];

function atMost(name, measured, limit) {
  return { name, measured, limit, isMet: measured !== null && measured <= limit };
}

function below(name, measured, limit) {
  return { name, measured, limit, isMet: measured !== null && measured < limit };
}

// A container killed for its memory, by its own exit or one of its processes (a worker, a
// backend), or restarted for any reason, during the run.
function killedOf(containers) {
  return WATCHED.filter((service) => {
    const health = containers[service]?.health;
    return !health || health.oomKilled || health.restarts > 0 || health.kills > 0;
  });
}

/**
 * Each criterion with what the run measured: the SSR and the API at their 95th percentile of
 * cores, no container killed, the probe's latencies (a run that measured none proves
 * nothing) and its failures (a refused human is not served).
 */
export function verdictOf({ containers, probe }) {
  const killed = killedOf(containers);
  const checks = [
    atMost('ssr-cores-p95', containers['web-ssr']?.cores.p95 ?? null, CRITERIA.ssrCores),
    atMost('api-cores-p95', containers.api?.cores.p95 ?? null, CRITERIA.apiCores),
    { name: 'no-oom', measured: killed, limit: [], isMet: killed.length === 0 },
    below('probe-hit-p95-ms', probe.hit.p95, CRITERIA.hitP95Ms),
    below('probe-miss-p95-ms', probe.miss.p95, CRITERIA.missP95Ms),
    below('probe-failed-rate', probe.failed.rate, CRITERIA.probeFailedRate),
  ];
  return { isMet: checks.every((check) => check.isMet), checks };
}
