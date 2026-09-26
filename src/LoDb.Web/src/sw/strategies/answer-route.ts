import type { SwRoute } from '../routing/sw-route';
import type { FetchEventLike } from '../scope/fetch-event-like';
import { openSlot } from '../storage/open-slot';
import { WORKER_CACHES } from '../storage/worker-caches';
import { answerFromCache } from './answer-from-cache';
import { answerPage } from './answer-page';

/** Answers a request the worker handles, with the strategy of its route. */
export async function answerRoute(
  route: Exclude<SwRoute, 'bypass'>,
  event: FetchEventLike,
  offlineCache: string,
): Promise<Response> {
  if (route === 'page') {
    const pages = await openSlot(WORKER_CACHES.pages);
    const offline = await caches.open(offlineCache).catch(() => undefined);
    return answerPage(event, pages, offline);
  }
  return answerFromCache(
    event,
    await openSlot(WORKER_CACHES[route === 'asset' ? 'assets' : 'blobs']),
  );
}
