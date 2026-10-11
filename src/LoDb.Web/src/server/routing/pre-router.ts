import type { RequestHandler, Response } from 'express';
import { NOINDEX_HEADERS } from '../../app/core/routing/response/noindex-headers';
import { documentPolicyHeaders } from '../security/csp/document-policy-headers';
import { NOT_FOUND_PAGE } from './not-found-page';
import type { PreRoute } from './pre-route';
import { preRouteOf } from './pre-route-of';
import type { QueryLimits } from './query-limits';

const MOVED_PERMANENTLY = 301;
const BAD_REQUEST = 400;
const NOT_FOUND = 404;
// A cleaned URL is a hop, not a page: no cache keeps it, so invented queries leave no trace.
const NEVER_STORED = 'no-store';
// No deployment turns an address outside the grammar into a page, and a new one changes the
// proxy's cache key anyway: the proxy keeps the answer an hour, a scanner's flood costs none.
const STATIC_NOT_FOUND_CACHE = 'public, max-age=0, s-maxage=3600';

function answer(route: Exclude<PreRoute, { kind: 'render' }>, response: Response): void {
  switch (route.kind) {
    case 'redirect':
      response.set('Cache-Control', NEVER_STORED).location(route.location);
      response.status(MOVED_PERMANENTLY).end();
      return;
    case 'not-found':
      response.set({
        ...NOINDEX_HEADERS,
        'Cache-Control': STATIC_NOT_FOUND_CACHE,
        ...documentPolicyHeaders([]),
      });
      response.status(NOT_FOUND).type('html').send(NOT_FOUND_PAGE);
      return;
    case 'bad-request':
      response.set('Cache-Control', NEVER_STORED);
      response.status(BAD_REQUEST).type('text/plain').send('Bad Request');
  }
}

/**
 * Answers before the render admission what the shape of a URL decides alone (`preRouteOf`):
 * the 400 of a path no page can have, the static 404 of an address outside the grammar,
 * the redirect of a list or a detail to its URL without the parameters it does not read.
 * None of them starts Angular nor takes a render slot; the other requests go on.
 * `onRedirect` is told of each redirect (`lodb_ssr_normalized_redirects_total`).
 */
export function preRouter(
  limits: QueryLimits,
  onRedirect: () => void = () => undefined,
): RequestHandler {
  return (request, response, next) => {
    const route = preRouteOf(request.originalUrl, limits);
    if (route.kind === 'render') {
      next();
      return;
    }
    if (route.kind === 'redirect') {
      onRedirect();
    }
    answer(route, response);
  };
}
