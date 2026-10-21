import { createServer, type IncomingMessage, type Server, type ServerResponse } from 'node:http';
import type { Logger } from 'pino';
import type { Registry } from 'prom-client';
import type { RefusedPagesReport } from '../demand/refused-pages-report';
import type { LoadReport } from '../load/load-report';

const METRICS_PATH = '/metrics';
const LOAD_PATH = '/load';
const REFUSED_PATH = '/refused';
const AFTER_PARAMETER = 'after';
// Only parses the query: the request's own host is never read.
const LOCAL_ORIGIN = 'http://metrics.invalid';
const OK = 200;
const NOT_FOUND = 404;
const INTERNAL_ERROR = 500;
const JSON_TYPE = 'application/json';
// A scrape is one small GET: a client that takes longer is not the collector.
const REQUEST_TIMEOUT_MS = 10_000;

/**
 * What the metrics port answers: the Prometheus scrape, the regulator's load and the pages of
 * older patches refused since the start, for the API's automatic queue.
 */
export interface MetricsEndpoints {
  readonly registry: Registry;
  readonly load: () => Promise<LoadReport>;
  readonly refused: (after: number) => RefusedPagesReport;
}

interface Body {
  readonly contentType: string;
  readonly text: string;
}

// A target URL that cannot parse (`//[`) is a 404 like any unknown path, never a thrown error.
function urlOf(request: IncomingMessage): URL | null {
  return request.method === 'GET' ? URL.parse(request.url ?? '', LOCAL_ORIGIN) : null;
}

// A cursor that is not a whole number reads from the start: the API diffs whole counts.
function afterOf(url: URL): number {
  const after = Number(url.searchParams.get(AFTER_PARAMETER) ?? 0);
  return Number.isSafeInteger(after) && after > 0 ? after : 0;
}

async function scrape(registry: Registry): Promise<Body> {
  return { contentType: registry.contentType, text: await registry.metrics() };
}

// Never through the registry: a collect resets prom-client's event loop monitor.
async function load(endpoints: MetricsEndpoints): Promise<Body> {
  return { contentType: JSON_TYPE, text: JSON.stringify(await endpoints.load()) };
}

function refused(endpoints: MetricsEndpoints, url: URL): Promise<Body> {
  const report = endpoints.refused(afterOf(url));
  return Promise.resolve({ contentType: JSON_TYPE, text: JSON.stringify(report) });
}

function bodyOf(endpoints: MetricsEndpoints, url: URL | null): Promise<Body> | null {
  switch (url?.pathname) {
    case METRICS_PATH:
      return scrape(endpoints.registry);
    case LOAD_PATH:
      return load(endpoints);
    case REFUSED_PATH:
      return refused(endpoints, url);
    default:
      return null;
  }
}

function handler(endpoints: MetricsEndpoints, logger: Logger) {
  return (request: IncomingMessage, response: ServerResponse): void => {
    const body = bodyOf(endpoints, urlOf(request));
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
 * regulator: not a Prometheus endpoint), `GET /refused?after={cursor}` (the refused pages of
 * older patches, as JSON, for the API's automatic queue) and a bare 404 to anything else.
 */
export function serveMetrics(endpoints: MetricsEndpoints, port: number, logger: Logger): Server {
  const server = createServer(handler(endpoints, logger));
  server.headersTimeout = REQUEST_TIMEOUT_MS;
  server.requestTimeout = REQUEST_TIMEOUT_MS;
  server.listen(port, () => logger.info({ port }, 'metrics.server.listening'));
  return server;
}
