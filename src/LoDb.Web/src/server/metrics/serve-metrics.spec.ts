import { once } from 'node:events';
import type { Server } from 'node:http';
import type { AddressInfo } from 'node:net';
import { Counter, Registry } from 'prom-client';
import { createLogger } from '../create-logger';
import { serveMetrics } from './serve-metrics';

const ANY_FREE_PORT = 0;

describe('serveMetrics', () => {
  let server: Server;
  let origin: string;

  beforeEach(async () => {
    const registry = new Registry();
    const counter = new Counter({ name: 'lodb_test_total', help: 'Test.', registers: [registry] });
    counter.inc();
    server = serveMetrics(registry, ANY_FREE_PORT, createLogger({ write: () => undefined }));
    await once(server, 'listening');
    origin = `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
  });

  afterEach(async () => {
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

  it.each([
    ['GET', '/'],
    ['GET', '/healthz'],
    ['GET', '/en/'],
    ['GET', '/metrics/lodb_test_total'],
    ['POST', '/metrics'],
  ])('answers %s %s with a bare 404', async (method, path) => {
    const response = await fetch(`${origin}${path}`, { method });

    expect(response.status).toBe(404);
    expect(await response.text()).toBe('');
  });
});
