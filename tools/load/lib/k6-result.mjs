// What a k6 run of bench.mjs measured, from the summary its handleSummary writes: requests by
// status, the human probe's latencies and failures, the warm-up streams' ends.

const STATUS_COUNT = /^http_reqs\{traffic:(\w+),status:(\d+)\}$/;
const NOT_LISTED = 'other';

function countOf(metric) {
  return metric?.values.count ?? 0;
}

function statusesOf(metrics, traffic) {
  const total = countOf(metrics[`http_reqs{traffic:${traffic}}`]);
  const byStatus = {};
  for (const [name, metric] of Object.entries(metrics)) {
    const match = STATUS_COUNT.exec(name);
    if (match?.[1] === traffic && countOf(metric) > 0) {
      byStatus[match[2]] = countOf(metric);
    }
  }
  const listed = Object.values(byStatus).reduce((sum, count) => sum + count, 0);
  if (total > listed) {
    byStatus[NOT_LISTED] = total - listed;
  }
  return { total, byStatus };
}

// A trend k6 never fed is absent from its summary: no sample, nothing measured.
function latencyOf(metric) {
  const count = metric?.values.count ?? 0;
  return { count, p95: count > 0 ? metric.values['p(95)'] : null };
}

function probeOf(metrics) {
  const failed = metrics.probe_failed?.values;
  return {
    hit: latencyOf(metrics.probe_hit_duration),
    miss: latencyOf(metrics.probe_miss_duration),
    failed: { count: failed?.passes ?? 0, rate: failed?.rate ?? null },
  };
}

/** The figures of a run, from the summary k6 handed to handleSummary. */
export function resultOf(summary) {
  const { metrics } = summary;
  return {
    load: statusesOf(metrics, 'load'),
    probeRequests: statusesOf(metrics, 'probe'),
    dropped: countOf(metrics.dropped_iterations),
    probe: probeOf(metrics),
    warmUps: {
      done: countOf(metrics.warm_up_done),
      failed: countOf(metrics.warm_up_failed),
      cut: countOf(metrics.warm_up_cut),
    },
  };
}
