import type { RequestHandler } from 'express';
import type { Logger } from 'pino';
import { traceIdFromTraceparent } from './trace-id-from-traceparent';

const FIRST_SERVER_ERROR = 500;

/**
 * Logs `http.request.served` once per response: method, status, duration and the trace id of
 * the proxy. Never the path, the query or the client address: a path can carry a username
 * (`/u/…`) or a share token (`/b/…`), and logs keep no personal data.
 */
export function requestLogging(logger: Logger): RequestHandler {
  return (request, response, next) => {
    const start = performance.now();
    response.once('finish', () => {
      const fields = {
        method: request.method,
        status: response.statusCode,
        duration_ms: Math.round(performance.now() - start),
        trace_id: traceIdFromTraceparent(request.get('traceparent')),
      };
      if (response.statusCode >= FIRST_SERVER_ERROR) {
        logger.error(fields, 'http.request.served');
      } else {
        logger.info(fields, 'http.request.served');
      }
    });
    next();
  };
}
