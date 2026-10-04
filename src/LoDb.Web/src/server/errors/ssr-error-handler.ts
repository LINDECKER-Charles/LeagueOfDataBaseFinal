import type { ErrorHandler } from '@angular/core';
import type { Logger } from 'pino';

/**
 * Replaces Angular's default handler during server renders, which prints
 * `console.error('ERROR', error)` as a multi-line stack that Vector splits into keyless
 * events. Every render error, an unknown URL's NG04002 included, becomes one
 * `ssr.render.failed` JSON line; the logger keeps the class and the code, never the message.
 */
export class SsrErrorHandler implements ErrorHandler {
  constructor(private readonly logger: Logger) {}

  handleError(error: unknown): void {
    this.logger.error({ exception: error }, 'ssr.render.failed');
  }
}
