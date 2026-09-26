import type { RequestFacts } from './request-facts';
import type { SwRoute } from './sw-route';

// Same-origin paths the worker never touches: the API, the back office, private pages (which
// must never outlive a sign-out in a cache), build shares, payment webhooks and the public API.
const BYPASSED_PATHS = [
  /^\/api(?:\/|$)/,
  /^\/admin(?:\/|$)/,
  /^\/[^/]+\/account(?:\/|$)/,
  /^\/b(?:\/|$)/,
  /^\/webhooks(?:\/|$)/,
  /^\/v1(?:\/|$)/,
];
// Hashed bundles (ADR 0005 reserves /build/ for them) and the fonts of public/.
const ASSET_PREFIXES = ['/build/', '/fonts/'];
// Content-addressed images of the storage volume: a key never changes its bytes.
const BLOB_PREFIX = '/cdn/blobs/';

function isBypassed(request: RequestFacts, url: URL, origin: string): boolean {
  // A Range request wants part of a body, and a stream must reach the page untouched.
  return (
    request.method !== 'GET' ||
    request.hasRange ||
    request.accept.includes('text/event-stream') ||
    url.origin !== origin ||
    BYPASSED_PATHS.some((path) => path.test(url.pathname))
  );
}

/**
 * Chooses the strategy of a request (heritage H4), `origin` being the worker's own. Pages are
 * navigations or fetches asking for HTML; anything else not listed goes to the network as if
 * there were no worker.
 */
export function routeRequest(request: RequestFacts, origin: string): SwRoute {
  const url = new URL(request.url);
  if (isBypassed(request, url, origin)) {
    return 'bypass';
  }
  if (url.pathname.startsWith(BLOB_PREFIX)) {
    return 'blob';
  }
  if (ASSET_PREFIXES.some((prefix) => url.pathname.startsWith(prefix))) {
    return 'asset';
  }
  const wantsPage = request.mode === 'navigate' || request.accept.includes('text/html');
  return wantsPage ? 'page' : 'bypass';
}
