// What the containers did over a run: their usage, from the `docker stats --no-stream
// --format '{{json .}}'` lines that run.sh samples every few seconds, and what their logs
// tell (the SSR's admissions, the API's warnings).

const SIZE = /^([\d.]+)\s*([a-z]+)$/i;
const UNITS = {
  B: 1,
  KB: 1e3,
  KIB: 2 ** 10,
  MB: 1e6,
  MIB: 2 ** 20,
  GB: 1e9,
  GIB: 2 ** 30,
  TB: 1e12,
  TIB: 2 ** 40,
};
const PERCENT = 100;
const MEDIAN = 0.5;
const HIGH = 0.95;
// lodb-dev-e1-web-ssr-1: the project, the service, the replica.
const REPLICA = /-\d+$/;

/** Bytes of a size as docker writes it ("234.5MiB"), null when unreadable. */
export function bytesOf(size) {
  const match = SIZE.exec(String(size ?? '').trim());
  const unit = match ? UNITS[match[2].toUpperCase()] : undefined;
  return unit === undefined ? null : Number(match[1]) * unit;
}

function sampleOf(stats, project) {
  const cores = Number.parseFloat(stats.CPUPerc) / PERCENT;
  const memory = bytesOf(String(stats.MemUsage ?? '').split('/')[0]);
  const prefix = `${project}-`;
  if (!Number.isFinite(cores) || memory === null || !stats.Name?.startsWith(prefix)) {
    return null;
  }
  return { service: stats.Name.slice(prefix.length).replace(REPLICA, ''), cores, memory };
}

/** The samples of each service of `project`: cores used and memory, line by line. */
export function samplesOf(lines, project) {
  const samples = new Map();
  for (const line of lines) {
    let stats;
    try {
      stats = JSON.parse(line);
    } catch {
      continue;
    }
    const sample = sampleOf(stats, project);
    if (sample !== null) {
      samples.set(sample.service, [...(samples.get(sample.service) ?? []), sample]);
    }
  }
  return samples;
}

/** The `fraction` quantile of numbers, by nearest rank; null without any. */
export function quantileOf(values, fraction) {
  if (values.length === 0) {
    return null;
  }
  const sorted = [...values].sort((a, b) => a - b);
  const rank = Math.ceil(fraction * sorted.length);
  return sorted[Math.min(sorted.length, Math.max(1, rank)) - 1];
}

function meanOf(values) {
  return values.length === 0 ? null : values.reduce((sum, value) => sum + value, 0) / values.length;
}

/** Cores (median, 95th percentile, mean, peak) and memory (95th percentile, peak) of a run. */
export function usageOf(samples) {
  const cores = samples.map((sample) => sample.cores);
  const memory = samples.map((sample) => sample.memory);
  return {
    samples: samples.length,
    cores: {
      p50: quantileOf(cores, MEDIAN),
      p95: quantileOf(cores, HIGH),
      mean: meanOf(cores),
      max: quantileOf(cores, 1),
    },
    memory: { p95: quantileOf(memory, HIGH), max: quantileOf(memory, 1) },
  };
}

function add(counts, key, count) {
  counts[key] = (counts[key] ?? 0) + count;
}

/**
 * The SSR's admissions over a run, summed from the summaries it logs once a minute
 * (`ssr.admission.summary`): renders admitted by lane, refused by lane and reason.
 */
export function admissionOf(lines) {
  const admission = { admitted: {}, refused: {} };
  for (const line of lines) {
    let summary;
    try {
      summary = JSON.parse(line);
    } catch {
      continue;
    }
    for (const [lane, count] of Object.entries(summary.admitted ?? {})) {
      add(admission.admitted, lane, count);
    }
    for (const [lane, reasons] of Object.entries(summary.refused ?? {})) {
      Object.entries(reasons).forEach(([reason, count]) =>
        add(admission.refused, `${lane}/${reason}`, count));
    }
  }
  return admission;
}

const EVENT_COUNT = /^\s*(\d+)\s+"EventName":"([^"]+)"$/;

/** The API's warnings and errors by event, from `uniq -c` lines ("  3 "EventName":"x""). */
export function eventsOf(lines) {
  return Object.fromEntries(lines
    .map((line) => EVENT_COUNT.exec(line))
    .filter((match) => match !== null)
    .map((match) => [match[2], Number(match[1])]));
}

const PHASE = /^# (\w+) (\d+)$/;
const SERVICE = /^@ (\S+)$/;
const COUNTER = /^(\w+) (\d+)$/;
const MICROSECONDS = 1e6;
const MILLISECONDS = 1e3;

// "# before <ms>", then "@ <service>" and its cpu.stat lines: { before: { at, services } }.
function phasesOf(lines) {
  const phases = {};
  let phase = null;
  let counters = null;
  for (const line of lines) {
    const [label, service, counter] = [PHASE, SERVICE, COUNTER].map((form) => form.exec(line));
    if (label) {
      phase = phases[label[1]] = { at: Number(label[2]), services: {} };
      counters = null;
    } else if (service && phase) {
      counters = phase.services[service[1]] = {};
    } else if (counter && counters) {
      counters[counter[1]] = Number(counter[2]);
    }
  }
  return phases;
}

/**
 * Each container's CPU over a run from its cgroup counters before and after (run.sh): mean
 * cores, share of the quota's periods it was throttled in, and seconds spent throttled.
 */
export function cpuOf(lines) {
  const { before, after } = phasesOf(lines);
  if (!before || !after || after.at <= before.at) {
    return {};
  }
  const seconds = (after.at - before.at) / MILLISECONDS;
  const services = Object.keys(after.services).filter((service) => before.services[service]);
  return Object.fromEntries(services.map((service) => {
    const [start, end] = [before.services[service], after.services[service]];
    const delta = (key) => (end[key] ?? 0) - (start[key] ?? 0);
    const periods = delta('nr_periods');
    return [service, {
      cores: delta('usage_usec') / MICROSECONDS / seconds,
      throttledShare: periods > 0 ? delta('nr_throttled') / periods : 0,
      throttledSeconds: delta('throttled_usec') / MICROSECONDS,
    }];
  }));
}
