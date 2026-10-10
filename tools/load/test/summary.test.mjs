import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { resultOf } from '../lib/k6-result.mjs';
import { renderReport } from '../lib/report.mjs';
import {
  admissionOf,
  bytesOf,
  cpuOf,
  eventsOf,
  quantileOf,
  samplesOf,
  usageOf,
} from '../lib/stats.mjs';
import { verdictOf } from '../lib/verdict.mjs';

const PROJECT = 'lodb-dev-e1';

function statsLine(service, cpu, memory) {
  return JSON.stringify({
    Name: `${PROJECT}-${service}-1`,
    CPUPerc: `${cpu}%`,
    MemUsage: `${memory} / 1.125GiB`,
  });
}

// What bench.mjs's handleSummary receives, cut down to the metrics the summary reads.
function k6Summary({ hitP95 = 40, missP95 = 900, failedRate = 0 } = {}) {
  const count = (value) => ({ values: { count: value } });
  return {
    metrics: {
      'http_reqs{traffic:load}': count(4140),
      'http_reqs{traffic:load,status:200}': count(140),
      'http_reqs{traffic:load,status:503}': count(3900),
      'http_reqs{traffic:load,status:404}': count(0),
      'http_reqs{traffic:probe}': count(180),
      'http_reqs{traffic:probe,status:200}': count(180),
      dropped_iterations: count(3),
      probe_hit_duration: { values: { count: 90, 'p(95)': hitP95 } },
      probe_miss_duration: { values: { count: 90, 'p(95)': missP95 } },
      probe_failed: { values: { rate: failedRate, passes: Math.round(failedRate * 180) } },
    },
  };
}

function containers(ssr, api, health = {}) {
  const usage = (p95) => ({ cores: { p50: p95 / 2, p95, mean: p95 / 2, max: p95 } });
  const ok = { oomKilled: false, restarts: 0, kills: 0 };
  return {
    'web-ssr': { ...usage(ssr), health: { ...ok, ...health } },
    api: { ...usage(api), health: ok },
    postgres: { ...usage(0.1), health: ok },
    nginx: { ...usage(0.05), health: ok },
  };
}

describe('docker stats', () => {
  test('reads the sizes docker writes', () => {
    assert.equal(bytesOf('512MiB'), 512 * 2 ** 20);
    assert.equal(bytesOf('1.5GiB'), 1.5 * 2 ** 30);
    assert.equal(bytesOf('20kB'), 20_000);
    assert.equal(bytesOf('--'), null);
  });

  test("groups the samples by service, and skips what is not the project's", () => {
    const samples = samplesOf([
      statsLine('web-ssr', '45.50', '200MiB'),
      statsLine('api', '120.00', '600MiB'),
      statsLine('web-ssr', '55.50', '210MiB'),
      JSON.stringify({ Name: 'lodb-dev-api-1', CPUPerc: '1%', MemUsage: '1MiB / 2GiB' }),
      'not json',
    ], PROJECT);
    assert.deepEqual([...samples.keys()], ['web-ssr', 'api']);
    assert.deepEqual(samples.get('web-ssr').map((sample) => sample.cores), [0.455, 0.555]);
    assert.equal(samples.get('api')[0].memory, 600 * 2 ** 20);
  });

  test('takes quantiles by nearest rank', () => {
    const values = Array.from({ length: 20 }, (_, index) => index + 1);
    assert.equal(quantileOf(values, 0.5), 10);
    assert.equal(quantileOf(values, 0.95), 19);
    assert.equal(quantileOf(values, 1), 20);
    assert.equal(quantileOf([], 0.95), null);
  });

  test('sums up the cores and the memory of a run', () => {
    const usage = usageOf([0.25, 0.5, 0.75].map((cores) => ({ cores, memory: cores * 1000 })));
    assert.deepEqual(usage.cores, { p50: 0.5, p95: 0.75, mean: 0.5, max: 0.75 });
    assert.deepEqual(usage.memory, { p95: 750, max: 750 });
    assert.equal(usage.samples, 3);
  });
});

describe('logs', () => {
  test("sums the SSR's admission summaries by lane and reason", () => {
    const summary = (admitted, refused) =>
      JSON.stringify({ msg: 'ssr.admission.summary', admitted, refused });
    const admission = admissionOf([
      summary({ current: 20, pinned: 32 }, { pinned: { lane_rate: 421, lane_capacity: 120 } }),
      summary({ pinned: 8 }, { pinned: { lane_rate: 79 }, current: { client_capacity: 3 } }),
      'not json',
    ]);
    assert.deepEqual(admission.admitted, { current: 20, pinned: 40 });
    assert.deepEqual(admission.refused,
      { 'pinned/lane_rate': 500, 'pinned/lane_capacity': 120, 'current/client_capacity': 3 });
  });

  test("counts the API's warnings by event", () => {
    assert.deepEqual(eventsOf([
      '     12 "EventName":"catalog.read.failed"',
      '      1 "EventName":"ingest.version.failed"',
      '',
    ]), { 'catalog.read.failed': 12, 'ingest.version.failed': 1 });
  });
});

describe('cgroup counters', () => {
  const counters = (service, usage, periods, throttled, throttledUsec) => [
    `@ ${service}`,
    `usage_usec ${usage}`,
    'user_usec 1',
    `nr_periods ${periods}`,
    `nr_throttled ${throttled}`,
    `throttled_usec ${throttledUsec}`,
  ];

  test('gives the mean cores and the throttling of each container over the run', () => {
    const cpu = cpuOf([
      '# before 1000000',
      ...counters('nginx', 1_000_000, 100, 1, 50_000),
      ...counters('api', 5_000_000, 100, 0, 0),
      '# after 1010000',
      ...counters('nginx', 3_500_000, 200, 26, 2_550_000),
      ...counters('api', 11_000_000, 200, 0, 0),
    ]);
    assert.deepEqual(cpu.nginx, { cores: 0.25, throttledShare: 0.25, throttledSeconds: 2.5 });
    assert.deepEqual(cpu.api, { cores: 0.6, throttledShare: 0, throttledSeconds: 0 });
  });

  test('measures nothing without both readings', () => {
    assert.deepEqual(cpuOf(['# before 1000', ...counters('api', 1, 1, 0, 0)]), {});
  });
});

describe('k6 result', () => {
  test('counts the requests by status, the probe apart, and what k6 dropped', () => {
    const result = resultOf(k6Summary());
    assert.deepEqual(result.load, { total: 4140, byStatus: { 200: 140, 503: 3900, other: 100 } });
    assert.deepEqual(result.probeRequests.byStatus, { 200: 180 });
    assert.equal(result.dropped, 3);
    assert.deepEqual(result.probe.hit, { count: 90, p95: 40 });
    assert.deepEqual(result.warmUps, { done: 0, failed: 0, cut: 0 });
  });

  test('measures nothing for a trend k6 never fed', () => {
    const summary = k6Summary();
    delete summary.metrics.probe_miss_duration;
    assert.deepEqual(resultOf(summary).probe.miss, { count: 0, p95: null });
  });
});

describe('verdict', () => {
  test('meets the criteria when the host holds and the human is served', () => {
    const { probe } = resultOf(k6Summary());
    const verdict = verdictOf({ probe, containers: containers(0.55, 0.4) });
    assert.equal(verdict.isMet, true);
  });

  test('misses on the cores of the SSR or the API, a killed container, a slow probe', () => {
    const { probe } = resultOf(k6Summary());
    const missed = (input) => verdictOf(input).checks.filter((check) => !check.isMet)
      .map((check) => check.name);
    assert.deepEqual(missed({ probe, containers: containers(0.9, 0.4) }), ['ssr-cores-p95']);
    assert.deepEqual(missed({ probe, containers: containers(0.3, 0.61) }), ['api-cores-p95']);
    assert.deepEqual(missed({ probe, containers: containers(0.3, 0.3, { restarts: 1 }) }),
      ['no-oom']);
    const slow = resultOf(k6Summary({ missP95: 2100, failedRate: 0.2 })).probe;
    assert.deepEqual(missed({ probe: slow, containers: containers(0.3, 0.3) }),
      ['probe-miss-p95-ms', 'probe-failed-rate']);
  });

  test('misses a criterion nothing measured', () => {
    const summary = k6Summary();
    delete summary.metrics.probe_hit_duration;
    const verdict = verdictOf({ probe: resultOf(summary).probe, containers: {} });
    assert.equal(verdict.isMet, false);
    assert.ok(verdict.checks.every((check) => check.name === 'probe-miss-p95-ms' ||
      check.name === 'probe-failed-rate' || !check.isMet));
  });
});

describe('report', () => {
  test('writes the synthesis and the details of each scenario', () => {
    const result = resultOf(k6Summary());
    const scenario = {
      name: 'S1',
      rate: 23,
      crowd: 237,
      result,
      containers: containers(0.9, 0.3),
    };
    scenario.verdict = verdictOf({ ...result, containers: scenario.containers });
    scenario.logs = {
      admission: { admitted: { pinned: 40 }, refused: { 'pinned/lane_rate': 500 } },
      apiEvents: {},
    };
    const quiet = {
      ...scenario,
      name: 'S3',
      logs: { admission: { admitted: {}, refused: {} }, apiEvents: { 'catalog.read.failed': 2 } },
    };
    const report = renderReport({
      run: {
        date: '2026-10-10T21:00Z',
        revision: 'abc1234',
        project: PROJECT,
        duration: '3m',
        warmUp: '30s',
        k6Image: 'grafana/k6:2.3.0@sha256:x',
        docker: { cpus: 24, memoryBytes: 32 * 2 ** 30 },
      },
      scenarios: [scenario, quiet],
    });
    assert.match(report, /^# Banc de charge k6/);
    assert.match(report, /\| S1 \| 4140 \| 0\.45 \/ 0\.90 \|/);
    assert.match(report, /## S1 — Pages épinglées uniques/);
    assert.match(report, /\*\*✗\*\* `ssr-cores-p95` : 0\.90 cœur \(borne 0\.60 cœur\)/);
    assert.match(report, /✓ `probe-miss-p95-ms` : 900 ms \(borne 1500 ms\)/);
    assert.match(report, /✓ `no-oom` : aucun \(borne aucun\)/);
    assert.match(report, /503: 3900/);
    assert.match(report, /refus\) : admis pinned 40 ; refusés pinned\/lane_rate 500/);
    assert.match(report, /## S3[^#]*Admission du SSR : aucun rendu refusé\./);
    assert.match(report, /## S3[^#]*de l'API : catalog\.read\.failed 2\./);
  });
});
