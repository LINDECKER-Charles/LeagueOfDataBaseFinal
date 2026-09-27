// Asks the new stack for one former URL and judges the answer against its expectation
// (legacy-urls.mjs): one hop, the new form, then a page that answers 200.

import { expectationOf, locationMatches } from './legacy-urls.mjs';

const OK = 200;
const MOVED_PERMANENTLY = 301;
const FOUND = 302;

async function head(fetcher, url, timeoutMs) {
  const response = await fetcher(url, {
    redirect: 'manual',
    headers: { 'Accept-Language': 'en' },
    signal: AbortSignal.timeout(timeoutMs),
  });
  // The body is not read: release it so the connection can be reused.
  await response.body?.cancel();
  return { status: response.status, location: response.headers.get('location') };
}

/**
 * The verdict for `path` on the stack at `base`: `{ path, ok, kind, status, location,
 * landing, reason }`. `fetcher` is `fetch`, replaced in tests.
 */
export async function verify(path, { base, timeoutMs, fetcher = fetch }) {
  const expectation = expectationOf(path);
  const verdict = { path, kind: expectation.description, ok: false };
  if (expectation.kind === 'unknown') {
    return { ...verdict, reason: 'no row of the 301 table matches this URL' };
  }

  let first;
  try {
    first = await head(fetcher, new URL(path, base), timeoutMs);
  } catch (error) {
    return { ...verdict, reason: `no answer: ${error.message}` };
  }
  Object.assign(verdict, first);

  if (expectation.kind === 'contract') {
    const ok = first.status !== MOVED_PERMANENTLY;
    return { ...verdict, ok, reason: ok ? undefined : 'a contract URL is redirected' };
  }
  if (expectation.kind === 'sitemap') {
    const ok = first.status === OK;
    return { ...verdict, ok, reason: ok ? undefined : `expected 200, got ${first.status}` };
  }

  const expectedStatus = expectation.kind === 'root' ? FOUND : MOVED_PERMANENTLY;
  if (first.status !== expectedStatus) {
    return { ...verdict, reason: `expected ${expectedStatus}, got ${first.status}` };
  }
  if (first.location === null || !locationMatches(expectation, first.location, base)) {
    return { ...verdict, reason: `Location ${first.location} is not ${expectation.pattern}` };
  }

  let landing;
  try {
    landing = await head(fetcher, new URL(first.location, base), timeoutMs);
  } catch (error) {
    return { ...verdict, reason: `no answer at ${first.location}: ${error.message}` };
  }
  verdict.landing = landing.status;
  const ok = landing.status === OK;
  return { ...verdict, ok, reason: ok ? undefined : `${first.location} answered ${landing.status}` };
}

/** Runs `task` over `items` with at most `concurrency` at once; results keep the order. */
export async function pool(items, concurrency, task) {
  const results = new Array(items.length);
  let next = 0;
  async function worker() {
    while (next < items.length) {
      const index = next;
      next += 1;
      results[index] = await task(items[index], index);
    }
  }
  await Promise.all(Array.from({ length: Math.min(concurrency, items.length) }, worker));
  return results;
}
