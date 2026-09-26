import type { Request, RequestHandler } from 'express';

// Pages and files are only ever read: the API, behind its own nginx location, takes writes.
const READ_METHODS = new Set(['GET', 'HEAD']);
// Far above any address of the site, filters in the query included.
const MAX_URL_LENGTH = 4096;
const REASONS: Readonly<Record<number, string>> = {
  405: 'Method Not Allowed',
  413: 'Content Too Large',
  414: 'URI Too Long',
};

function refusalOf(request: Request): number | undefined {
  if (!READ_METHODS.has(request.method)) {
    return 405;
  }
  if (request.originalUrl.length > MAX_URL_LENGTH) {
    return 414;
  }
  const length = request.get('Content-Length');
  const hasBody = (length !== undefined && length !== '0') || request.get('Transfer-Encoding');
  return hasBody ? 413 : undefined;
}

/**
 * Refuses what no page needs, before anything renders: a method other than GET or HEAD
 * (405), an address longer than `MAX_URL_LENGTH` (414), a body (413). Such answers are plain
 * text, never stored, and close the connection instead of reading a body nobody wants.
 */
export function boundedRequests(): RequestHandler {
  return (request, response, next) => {
    const refusal = refusalOf(request);
    if (refusal === undefined) {
      next();
      return;
    }
    if (refusal === 405) {
      response.set('Allow', 'GET, HEAD');
    }
    response.set({ 'Cache-Control': 'no-store', Connection: 'close' });
    response.status(refusal).type('text/plain').send(REASONS[refusal]);
  };
}
