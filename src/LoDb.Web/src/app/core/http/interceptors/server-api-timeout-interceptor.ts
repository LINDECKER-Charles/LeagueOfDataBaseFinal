import { isPlatformServer } from '@angular/common';
import type { HttpInterceptorFn } from '@angular/common/http';
import { PLATFORM_ID, inject } from '@angular/core';
import { API_BASE_URL } from '../../api/api-base-url';
import { isApiRequest } from './is-api-request';

// Longer than a cold version's synchronous ingestion (Data Dragon attempts time out at 15 s),
// shorter than the patience of a visitor or of the proxy in front of the renderer.
const SERVER_API_TIMEOUT_MS = 30_000;

/**
 * Bounds each API call of a server render. A stuck API would otherwise hold the render, and
 * the proxy's connection, until a socket gives up; aborted, the call fails like an
 * unreachable API (status 0) and the page answers a 503 (`handleNavigationError`). The
 * browser keeps no bound: a visitor can leave, and uploads may legitimately take longer.
 */
export const serverApiTimeoutInterceptor: HttpInterceptorFn = (request, next) => {
  const bounded =
    isPlatformServer(inject(PLATFORM_ID)) &&
    request.timeout === undefined &&
    isApiRequest(request.url, inject(API_BASE_URL));
  return next(bounded ? request.clone({ timeout: SERVER_API_TIMEOUT_MS }) : request);
};
