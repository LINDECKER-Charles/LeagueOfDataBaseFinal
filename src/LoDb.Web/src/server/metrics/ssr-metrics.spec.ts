import { EventEmitter } from 'node:events';
import type { ServerResponse } from 'node:http';
import { RenderCounts } from '../admission/render-counts';
import type { RenderLane } from '../admission/render-lane';
import { SsrMetrics } from './ssr-metrics';

const CONTRACT_PREFIX = 'lodb_ssr_';
// The labels of the metrics contract (docs/guides/metriques.md): never a path, a version, a
// language, an address or a user agent.
const CONTRACT_LABELS = new Set(['lane', 'outcome', 'reason', 'status_class', 'le']);
const SAMPLE = /^([a-z_]+)(?:\{(.*)\})? (\S+)$/;
const LABEL = /(\w+)="([^"]*)"/g;

type Labels = Record<string, string>;

function labelsOf(text: string | undefined): Labels {
  return Object.fromEntries(
    [...(text ?? '').matchAll(LABEL)].map(([, name, value]) => [name, value]),
  );
}

/** Value of the sample of `name` with exactly these labels, in a Prometheus exposition. */
function sampleOf(exposition: string, name: string, labels: Labels = {}): number | undefined {
  const wanted = JSON.stringify(Object.entries(labels).sort());
  for (const line of exposition.split('\n')) {
    const match = SAMPLE.exec(line);
    if (
      match?.[1] === name &&
      JSON.stringify(Object.entries(labelsOf(match[2])).sort()) === wanted
    ) {
      return Number(match[3]);
    }
  }
  return undefined;
}

// A response that ends: `finished` when its whole answer was written, else its client left.
function responseOf(statusCode: number, finished: boolean): ServerResponse {
  const response = Object.assign(new EventEmitter(), { statusCode, writableFinished: finished });
  return response as unknown as ServerResponse;
}

describe('SsrMetrics', () => {
  let counts: RenderCounts;
  let inFlight: Record<RenderLane, number>;
  let metrics: SsrMetrics;
  const scrape = () => metrics.registry.metrics();

  beforeEach(() => {
    counts = new RenderCounts();
    inFlight = { pinned: 0, current: 0 };
    metrics = new SsrMetrics(counts, { inFlightOf: (lane) => inFlight[lane] });
  });

  it('exports the decisions of the admission as it counts them, never twice', async () => {
    counts.record('current', null);
    counts.record('current', null);
    counts.record('pinned', 'lane_rate');

    const first = await scrape();
    counts.record('current', null);
    const second = await scrape();

    const total = 'lodb_ssr_renders_total';
    expect(sampleOf(first, total, { lane: 'current', outcome: 'admitted' })).toBe(2);
    expect(sampleOf(first, total, { lane: 'pinned', outcome: 'admitted' })).toBe(0);
    expect(
      sampleOf(first, total, { lane: 'pinned', outcome: 'refused', reason: 'lane_rate' }),
    ).toBe(1);
    expect(sampleOf(second, total, { lane: 'current', outcome: 'admitted' })).toBe(3);
  });

  it('reads the renders in flight of each lane at the scrape', async () => {
    inFlight = { pinned: 1, current: 3 };

    const exposition = await scrape();

    expect(sampleOf(exposition, 'lodb_ssr_renders_in_flight', { lane: 'pinned' })).toBe(1);
    expect(sampleOf(exposition, 'lodb_ssr_renders_in_flight', { lane: 'current' })).toBe(3);
  });

  it('times each render until its response ends, by lane and status class', async () => {
    const served = responseOf(200, true);
    const abandoned = responseOf(200, false);
    const redirected = responseOf(301, true);

    metrics.timeRender('current', served);
    metrics.timeRender('pinned', abandoned);
    metrics.timeRender('current', redirected);
    [served, abandoned, redirected].forEach((response) => response.emit('close'));
    const exposition = await scrape();

    const count = 'lodb_ssr_render_duration_seconds_count';
    expect(sampleOf(exposition, count, { lane: 'current', status_class: '2xx' })).toBe(1);
    expect(sampleOf(exposition, count, { lane: 'current', status_class: '3xx' })).toBe(1);
    expect(sampleOf(exposition, count, { lane: 'pinned', status_class: 'aborted' })).toBe(1);
    const bucket = 'lodb_ssr_render_duration_seconds_bucket';
    expect(sampleOf(exposition, bucket, { lane: 'current', status_class: '2xx', le: '1.5' })).toBe(
      1,
    );
  });

  it('counts the redirects to the URL a page is known by, per reason, from zero', async () => {
    const redirects = 'lodb_ssr_normalized_redirects_total';
    expect(sampleOf(await scrape(), redirects, { reason: 'query' })).toBe(0);
    expect(sampleOf(await scrape(), redirects, { reason: 'slash' })).toBe(0);
    expect(sampleOf(await scrape(), redirects, { reason: 'canonical' })).toBe(0);

    metrics.countRedirect('query');
    [301, 308, 302, 200, 404].forEach((status) => metrics.countRendered(status));
    const exposition = await scrape();

    expect(sampleOf(exposition, redirects, { reason: 'query' })).toBe(1);
    expect(sampleOf(exposition, redirects, { reason: 'canonical' })).toBe(2);
  });

  it('counts the refusals of the network gate, from zero', async () => {
    const refusals = 'lodb_ssr_network_gate_refusals_total';
    expect(sampleOf(await scrape(), refusals)).toBe(0);

    metrics.countGateRefusal();
    metrics.countGateRefusal();

    expect(sampleOf(await scrape(), refusals)).toBe(2);
  });

  it("counts the inline scripts the pages' policy left out, from zero", async () => {
    const left = 'lodb_ssr_csp_unexpected_scripts_total';
    expect(sampleOf(await scrape(), left)).toBe(0);

    metrics.countUnexpectedScripts(3);

    expect(sampleOf(await scrape(), left)).toBe(3);
  });

  it("serves Node's own metrics, and no label outside the contract", async () => {
    counts.record('pinned', 'client_capacity');
    metrics.timeRender('pinned', responseOf(503, true));

    const exposition = await scrape();

    expect(exposition).toMatch(/^nodejs_eventloop_lag_seconds /m);
    expect(exposition).toMatch(/^nodejs_heap_size_used_bytes /m);
    const samples = exposition
      .split('\n')
      .map((line) => SAMPLE.exec(line))
      .filter((match) => match?.[1].startsWith(CONTRACT_PREFIX));
    expect(samples.length).toBeGreaterThan(0);
    for (const match of samples) {
      for (const label of Object.keys(labelsOf(match?.[2]))) {
        expect(CONTRACT_LABELS).toContain(label);
      }
    }
  });
});
