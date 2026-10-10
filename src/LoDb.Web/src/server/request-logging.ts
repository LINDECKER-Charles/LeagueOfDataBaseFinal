import type { RequestHandler } from 'express';
import type { Level, Logger } from 'pino';
import { SHED_MARKER } from './admission/shed-marker';
import { traceIdFromTraceparent } from './trace-id-from-traceparent';

const FIRST_SERVER_ERROR = 500;
const SERVICE_UNAVAILABLE = 503;

// A 503 is load shed on purpose, by the renderer or by the API, never an outage: a warning.
function levelOf(status: number): Level {
  if (status === SERVICE_UNAVAILABLE) {
    return 'warn';
  }
  return status >= FIRST_SERVER_ERROR ? 'error' : 'info';
}

/**
 * Logs `http.request.served` once per response: method, status, duration and the trace id of
 * the proxy. Never the path, the query or the client address: a path can carry a username
 * (`/u/…`) or a share token (`/b/…`), and logs keep no personal data. A render the admission
 * refused gets no line: the minute summary counts it (`AdmissionSummary`), so that a crawl
 * turned away does not flood the logs.
 */
export function requestLogging(logger: Logger): RequestHandler {
  return (request, response, next) => {
    const start = performance.now();
    response.once('finish', () => {
      if (response.locals[SHED_MARKER] === true) {
        return;
      }
      const fields = {
        method: request.method,
        status: response.statusCode,
        duration_ms: Math.round(performance.now() - start),
        trace_id: traceIdFromTraceparent(request.get('traceparent')),
      };
      logger[levelOf(response.statusCode)](fields, 'http.request.served');
    });
    next();
  };
}
