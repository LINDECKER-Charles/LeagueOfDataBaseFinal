import { createServer, type IncomingMessage, type Server, type ServerResponse } from 'node:http';
import type { Logger } from 'pino';
import type { Registry } from 'prom-client';

const METRICS_PATH = '/metrics';
const QUERY_START = '?';
const OK = 200;
const NOT_FOUND = 404;
const INTERNAL_ERROR = 500;
// A scrape is one small GET: a client that takes longer is not the collector.
const REQUEST_TIMEOUT_MS = 10_000;

function isScrape(request: IncomingMessage): boolean {
  const path = (request.url ?? '').split(QUERY_START, 1)[0];
  return request.method === 'GET' && path === METRICS_PATH;
}

function scrapeHandler(registry: Registry, logger: Logger) {
  return (request: IncomingMessage, response: ServerResponse): void => {
    if (!isScrape(request)) {
      response.writeHead(NOT_FOUND).end();
      return;
    }
    registry.metrics().then(
      (body) => {
        const headers = { 'Content-Type': registry.contentType, 'Cache-Control': 'no-store' };
        response.writeHead(OK, headers).end(body);
      },
      (error: unknown) => {
        logger.error({ exception: error }, 'metrics.scrape.failed');
        response.writeHead(INTERNAL_ERROR).end();
      },
    );
  };
}

/**
 * Serves the Prometheus scrape of `registry` on a port of its own, never the site's: nginx
 * proxies only the site's port and no host port publishes this one, so only the stack's
 * network and the host's collector (the shared `observability` network) reach it. It answers
 * `GET /metrics` and a bare 404 to anything else.
 */
export function serveMetrics(registry: Registry, port: number, logger: Logger): Server {
  const server = createServer(scrapeHandler(registry, logger));
  server.headersTimeout = REQUEST_TIMEOUT_MS;
  server.requestTimeout = REQUEST_TIMEOUT_MS;
  server.listen(port, () => logger.info({ port }, 'metrics.server.listening'));
  return server;
}
