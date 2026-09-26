// The figures of a page over its runs: the median of each number, and the details (failing
// audits, leads, uncompressed resources) of the run whose performance is the median one.

import { NUMERIC } from './metrics.mjs';

/** The median of numbers, the mean of the two middle ones for an even count. */
export function median(values) {
  const sorted = values.filter((value) => typeof value === 'number').sort((a, b) => a - b);
  if (sorted.length === 0) return null;
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
}

function medianRun(runs) {
  const target = median(runs.map((run) => run.performance));
  return runs.reduce((best, run) =>
    Math.abs(run.performance - target) < Math.abs(best.performance - target) ? run : best,
  );
}

/** The median figures of a page's runs, with the details of its median run. */
export function summarize(runs) {
  const typical = medianRun(runs);
  return {
    ...Object.fromEntries(NUMERIC.map((key) => [key, median(runs.map((run) => run[key]))])),
    runs: runs.length,
    failing: typical.failing,
    leads: typical.leads,
    uncompressed: typical.uncompressed,
    warnings: [...new Set(runs.flatMap((run) => run.warnings))],
    environment: typical.environment,
  };
}
