import type { ErrorRequestHandler } from 'express';
import type { Logger } from 'pino';

/**
 * Last Express handler: logs the failure as one JSON line, where Express would print a
 * multi-line stack that Vector splits into keyless events, and answers a bare 500.
 */
export function failureHandler(logger: Logger): ErrorRequestHandler {
  // Express only recognises an error handler by its four parameters.
  // eslint-disable-next-line max-params
  return (error: unknown, _request, response, _next) => {
    logger.error({ exception: error }, 'http.request.failed');
    if (response.headersSent) {
      response.destroy();
      return;
    }
    response.status(500).type('text/plain').send('Internal Server Error');
  };
}
