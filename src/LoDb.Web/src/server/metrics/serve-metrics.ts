import { createServer, type IncomingMessage, type Server, type ServerResponse } from 'node:http';
import type { Logger } from 'pino';
import type { Registry } from 'prom-client';
import type { LoadReport } from '../load/load-report';

const METRICS_PATH = '/metrics';
const LOAD_PATH = '/load';
const QUERY_START = '?';
const OK = 200;
const NOT_FOUND = 404;
const INTERNAL_ERROR = 500;
const JSON_TYPE = 'application/json';
// A scrape is one small GET: a client that takes longer is not the collector.
const REQUEST_TIMEOUT_MS = 10_000;

/** What the metrics port answers: the Prometheus scrape, and the regulator's load. */
export interface MetricsEndpoints {
  readonly registry: Registry;
  readonly load: () => Promise<LoadReport>;
}

interface Body {
  readonly contentType: string;
  readonly text: string;
}

function pathOf(request: IncomingMessage): string | null {
  return request.method === 'GET' ? (request.url ?? '').split(QUERY_START, 1)[0] : null;
}

async function scrape(registry: Registry): Promise<Body> {
  return { contentType: registry.contentType, text: await registry.metrics() };
}

// Never through the registry: a collect resets prom-client's event loop monitor.
async function load(endpoints: MetricsEndpoints): Promise<Body> {
  return { contentType: JSON_TYPE, text: JSON.stringify(await endpoints.load()) };
}

function bodyOf(endpoints: MetricsEndpoints, path: string | null): Promise<Body> | null {
  if (path === METRICS_PATH) {
    return scrape(endpoints.registry);
  }
  return path === LOAD_PATH ? load(endpoints) : null;
}

function handler(endpoints: MetricsEndpoints, logger: Logger) {
  return (request: IncomingMessage, response: ServerResponse): void => {
    const body = bodyOf(endpoints, pathOf(request));
    if (body === null) {
      response.writeHead(NOT_FOUND).end();
      return;
    }
    body.then(
      ({ contentType, text }) => {
        const headers = { 'Content-Type': contentType, 'Cache-Control': 'no-store' };
        response.writeHead(OK, headers).end(text);
      },
      (error: unknown) => {
        logger.error({ exception: error }, 'metrics.scrape.failed');
        response.writeHead(INTERNAL_ERROR).end();
      },
    );
  };
}

/**
 * Serves the Prometheus scrape of the registry on a port of its own, never the site's: nginx
 * proxies only the site's port and no host port publishes this one, so only the stack's
 * network and the host's collector (the shared `observability` network) reach it. It answers
 * `GET /metrics`, `GET /load` (the renderer's load, as JSON, for the API's pre-render
 * regulator: not a Prometheus endpoint) and a bare 404 to anything else.
 */
export function serveMetrics(endpoints: MetricsEndpoints, port: number, logger: Logger): Server {
  const server = createServer(handler(endpoints, logger));
  server.headersTimeout = REQUEST_TIMEOUT_MS;
  server.requestTimeout = REQUEST_TIMEOUT_MS;
  server.listen(port, () => logger.info({ port }, 'metrics.server.listening'));
  return server;
}
