import { REQUEST, inject } from '@angular/core';

/**
 * Public origin of the page being rendered, as the browser sees it, or null outside an SSR
 * request. Behind nginx it is only right when `LODB_TRUST_PROXY_HEADERS` lets Angular read
 * `X-Forwarded-Proto`.
 */
export function injectPageOrigin(): string | null {
  const request = inject(REQUEST, { optional: true });
  return request ? new URL(request.url).origin : null;
}
