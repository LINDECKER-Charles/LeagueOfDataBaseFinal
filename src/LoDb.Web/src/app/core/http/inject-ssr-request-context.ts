import { REQUEST_CONTEXT, inject } from '@angular/core';
import type { SsrRequestContext } from './ssr-request-context';

/**
 * The context of the current SSR request, or null outside one (browser, build-time route
 * extraction). `src/server.ts` is its only producer, hence the unchecked type.
 */
export function injectSsrRequestContext(): SsrRequestContext | null {
  return inject(REQUEST_CONTEXT, { optional: true }) as SsrRequestContext | null;
}
