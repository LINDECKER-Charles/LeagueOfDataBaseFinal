import type { Server } from 'node:http';
import { applyServerTimeouts } from './apply-server-timeouts';

describe('applyServerTimeouts', () => {
  it('bounds headers and requests, and outlives the idle connections of nginx', () => {
    const server = {} as Server;
    const nginxKeepaliveTimeout = 60_000;

    applyServerTimeouts(server);

    expect(server.headersTimeout).toBe(10_000);
    expect(server.requestTimeout).toBe(30_000);
    expect(server.keepAliveTimeout).toBeGreaterThan(nginxKeepaliveTimeout);
  });
});
