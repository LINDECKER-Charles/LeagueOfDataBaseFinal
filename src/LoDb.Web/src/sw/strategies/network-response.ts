import type { FetchEventLike } from '../scope/fetch-event-like';

/**
 * The network's answer to a request: the navigation preload the browser sent while the worker
 * was starting, when it sent one, else a fetch of the worker's own. Never both: a preload
 * left unread would have the page go out twice.
 */
export async function networkResponse(event: FetchEventLike): Promise<Response> {
  const preloaded = await event.preloadResponse;
  return preloaded ?? fetch(event.request);
}
