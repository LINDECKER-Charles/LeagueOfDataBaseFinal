import { once } from 'node:events';
import type { Server } from 'node:http';
import type { AddressInfo } from 'node:net';
import { monitorEventLoopDelay } from 'node:perf_hooks';
import { Counter, Registry } from 'prom-client';
import { RenderCounts } from '../admission/render-counts';
import { createLogger } from '../create-logger';
import type { LoadReport } from '../load/load-report';
import { serveMetrics } from './serve-metrics';
import { SsrMetrics } from './ssr-metrics';

const ANY_FREE_PORT = 0;
const IDLE_LANE = { renders: 0, renderSeconds: 0, refused: 0 };
const REPORT: LoadReport = {
  cpuSeconds: 1.5,
  eventLoopDelayP99Seconds: null,
  cpuPressureSomeAvg10: 0.02,
  cpuThrottledSeconds: null,
  lanes: { current: IDLE_LANE, pinned: IDLE_LANE, prerender: IDLE_LANE },
};

async function listen(registry: Registry): Promise<{ server: Server; origin: string }> {
  const logger = createLogger({ write: () => undefined });
  const server = serveMetrics({ registry, load: async () => REPORT }, ANY_FREE_PORT, logger);
  await once(server, 'listening');
  return { server, origin: `http://127.0.0.1:${(server.address() as AddressInfo).port}` };
}

describe('serveMetrics', () => {
  let server: Server;
  let origin: string;

  beforeEach(async () => {
    const registry = new Registry();
    const counter = new Counter({ name: 'lodb_test_total', help: 'Test.', registers: [registry] });
    counter.inc();
    ({ server, origin } = await listen(registry));
  });

  afterEach(async () => {
    vi.restoreAllMocks();
    server.close();
    await once(server, 'close');
  });

  it('answers the scrape in the Prometheus text format, never stored', async () => {
    const response = await fetch(`${origin}/metrics`);

    expect(response.status).toBe(200);
    expect(response.headers.get('content-type')).toMatch(/^text\/plain; version=0\.0\.4/);
    expect(response.headers.get('cache-control')).toBe('no-store');
    expect(await response.text()).toContain('lodb_test_total 1');
  });

  it('reads the path without its query', async () => {
    const response = await fetch(`${origin}/metrics?name[]=lodb_test_total`);

    expect(response.status).toBe(200);
  });

  it("answers the renderer's load as JSON, never stored", async () => {
    const response = await fetch(`${origin}/load`);

    expect(response.status).toBe(200);
    expect(response.headers.get('content-type')).toBe('application/json');
    expect(response.headers.get('cache-control')).toBe('no-store');
    expect(await response.json()).toEqual(REPORT);
  });

  it.each([
    ['GET', '/'],
    ['GET', '/healthz'],
    ['GET', '/en/'],
    ['GET', '/metrics/lodb_test_total'],
    ['GET', '/load/current'],
    ['POST', '/metrics'],
    ['POST', '/load'],
  ])('answers %s %s with a bare 404', async (method, path) => {
    const response = await fetch(`${origin}${path}`, { method });

    expect(response.status).toBe(404);
    expect(await response.text()).toBe('');
  });
});

describe('serveMetrics, beside the event loop gauges of /metrics', () => {
  it("never resets prom-client's event loop monitor on a read of /load", async () => {
    const metrics = new SsrMetrics(new RenderCounts(), { inFlightOf: () => 0 });
    const histogram = Object.getPrototypeOf(monitorEventLoopDelay()) as { reset(): void };
    const reset = vi.spyOn(histogram, 'reset');
    const { server, origin } = await listen(metrics.registry);
    const collect = vi.spyOn(metrics.registry, 'metrics');

    await fetch(`${origin}/load`);
    const afterLoad = reset.mock.calls.length;
    await fetch(`${origin}/metrics`);

    expect(afterLoad).toBe(0);
    expect(collect).toHaveBeenCalledTimes(1);
    expect(reset.mock.calls.length).toBeGreaterThan(0);
    reset.mockRestore();
    server.close();
    await once(server, 'close');
  });
});
