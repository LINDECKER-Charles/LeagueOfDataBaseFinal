import type { FetchEventLike } from '../scope/fetch-event-like';
import type { CacheSlot } from '../storage/cache-slot';
import { storeCopy } from '../storage/store-copy';
import { lastResortPage } from './last-resort-page';
import { OFFLINE_URL } from './offline-url';
import { within } from './within';

// How long a slow network may keep the visitor waiting when a copy of the page could answer.
// Without a copy there is no timeout: cutting a slow first visit would only break it (H4).
const NETWORK_DELAY_MS = 4000;

async function copyOrFallback(
  request: Request,
  pages: CacheSlot | undefined,
  offline: Cache | undefined,
): Promise<Response> {
  const copy = await pages?.cache.match(request);
  const offlinePage = copy ?? (await offline?.match(OFFLINE_URL));
  return offlinePage ?? lastResortPage();
}

/**
 * Network first for a page: the fresh page, kept as a copy; past `NETWORK_DELAY_MS`, or
 * offline, the last copy of that page; then the offline page. Always a Response: answering
 * undefined is what gave the previous worker its blank pages (heritage H4).
 */
export async function answerPage(
  event: FetchEventLike,
  pages: CacheSlot | undefined,
  offline: Cache | undefined,
): Promise<Response> {
  const network = fetch(event.request);
  // Registered before anything reads the response, so the copy is cloned from an unread body.
  // A failed fetch or a copy that cannot be stored is not an error of the answer.
  const stored = network.then((response) => storeCopy(pages, event.request, response));
  event.waitUntil(stored.catch(() => undefined));
  const copy = await pages?.cache.match(event.request);
  try {
    return await (copy === undefined ? network : within(network, NETWORK_DELAY_MS, copy));
  } catch {
    return copyOrFallback(event.request, pages, offline);
  }
}
