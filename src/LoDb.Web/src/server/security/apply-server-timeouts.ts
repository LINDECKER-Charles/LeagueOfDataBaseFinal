import type { Server } from 'node:http';

/**
 * Bounds how long a client may hold the server. Headers must arrive within 10 s and the whole
 * request within 30 s, so slow clients cannot pile up connections. Idle keep-alive connections
 * live 65 s: longer than nginx keeps its own (60 s), so nginx always closes first and never
 * reuses a connection this server has just closed (a 502 otherwise).
 */
export function applyServerTimeouts(server: Server): void {
  server.headersTimeout = 10_000;
  server.requestTimeout = 30_000;
  server.keepAliveTimeout = 65_000;
}
