import type { RequestFacts } from './request-facts';

/**
 * Whether the browser preloads this request while the worker starts: a GET navigation. A
 * navigation the worker otherwise leaves to the browser is still answered with that preload.
 */
export function isPreloadedNavigation(request: RequestFacts): boolean {
  return request.mode === 'navigate' && request.method === 'GET';
}
