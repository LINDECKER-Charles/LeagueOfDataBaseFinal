import { isPlatformServer } from '@angular/common';
import type { HttpInterceptorFn } from '@angular/common/http';
import { PLATFORM_ID, inject } from '@angular/core';
import { API_BASE_URL } from '../../api/api-base-url';
import { injectSsrRequestContext } from '../inject-ssr-request-context';
import type { RenderBudget } from '../render-budget';
import { isApiRequest } from './is-api-request';

// A render outside `src/server.ts`'s, which sets the budget from its environment: the same
// bound as the server's default for one call.
const DEFAULT_CALL_TIMEOUT_MS = 10_000;
// A render past its deadline still asks: its call fails at once, as a timed out one.
const SHORTEST_TIMEOUT_MS = 1;

function timeoutOf(budget: RenderBudget | undefined): number {
  if (budget === undefined) {
    return DEFAULT_CALL_TIMEOUT_MS;
  }
  const left = budget.deadline - Date.now();
  return Math.max(SHORTEST_TIMEOUT_MS, Math.min(budget.callTimeoutMs, left));
}

/**
 * Bounds each API call of a server render by the render's budget: the time left before its
 * deadline, and never more than one call's share. A slow API would otherwise hold the render,
 * its admission slot and the proxy's connection; aborted, the call fails like an unreachable
 * API (status 0) and the page answers a 502 (`handleNavigationError`), which the proxy covers
 * with its stale copy. The browser keeps no bound: a visitor can leave, and uploads may
 * legitimately take longer.
 */
export const serverApiTimeoutInterceptor: HttpInterceptorFn = (request, next) => {
  const bounded =
    isPlatformServer(inject(PLATFORM_ID)) &&
    request.timeout === undefined &&
    isApiRequest(request.url, inject(API_BASE_URL));
  if (!bounded) {
    return next(request);
  }
  const timeout = timeoutOf(injectSsrRequestContext()?.renderBudget);
  return next(request.clone({ timeout }));
};
