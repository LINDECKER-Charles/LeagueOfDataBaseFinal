import type { RequestFacts } from './request-facts';

/** Reads the routing facts of a request. */
export function requestFactsOf(request: Request): RequestFacts {
  return {
    method: request.method,
    url: request.url,
    mode: request.mode,
    accept: request.headers.get('Accept') ?? '',
    hasRange: request.headers.has('Range'),
  };
}
