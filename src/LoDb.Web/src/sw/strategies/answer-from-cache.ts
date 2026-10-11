import type { FetchEventLike } from '../scope/fetch-event-like';
import type { CacheSlot } from '../storage/cache-slot';
import { storeCopy } from '../storage/store-copy';
import { networkResponse } from './network-response';

/**
 * Cache first, for files whose URL never changes its bytes (hashed bundles, fonts, blobs):
 * the copy when there is one, else the network, kept as a copy. Offline and uncached, the
 * request fails like a network error rather than leaving the promise rejected. A file opened
 * as a page (an image in its own tab) takes its navigation preload, never a second fetch.
 */
export async function answerFromCache(
  event: FetchEventLike,
  slot: CacheSlot | undefined,
): Promise<Response> {
  const copy = await slot?.cache.match(event.request);
  if (copy !== undefined) {
    return copy;
  }
  try {
    const response = await networkResponse(event);
    event.waitUntil(storeCopy(slot, event.request, response).catch(() => undefined));
    return response;
  } catch {
    return Response.error();
  }
}
