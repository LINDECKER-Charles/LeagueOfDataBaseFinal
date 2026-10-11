import { isPreloadedNavigation } from './routing/is-preloaded-navigation';
import { requestFactsOf } from './routing/request-facts-of';
import { routeRequest } from './routing/route-request';
import type { WorkerScope } from './scope/worker-scope';
import { deleteStaleCaches } from './storage/delete-stale-caches';
import { CACHE_PREFIX, WORKER_CACHES } from './storage/worker-caches';
import { answerRoute } from './strategies/answer-route';
import { networkResponse } from './strategies/network-response';
import { OFFLINE_URL } from './strategies/offline-url';

// Entry of /sw.js, bundled after the `web` build (bundle-worker.mjs), which replaces this
// constant with a hash of public/offline.html: a new offline page is a new worker.
declare const LODB_OFFLINE_REVISION: string;

const scope = self as unknown as WorkerScope;
const offlineCache = `${CACHE_PREFIX}offline-${LODB_OFFLINE_REVISION}`;
const keptCaches = [offlineCache, ...Object.values(WORKER_CACHES).map((spec) => spec.name)];

scope.addEventListener('install', (event) => {
  const precached = caches.open(offlineCache).then((cache) => cache.add(OFFLINE_URL));
  event.waitUntil(precached.then(() => scope.skipWaiting()));
});

scope.addEventListener('activate', (event) => {
  // A navigation then leaves for the network while the worker starts, instead of after it.
  // Every navigation the worker sees is answered with that preload (networkResponse), the
  // ones it otherwise leaves to the browser included: none goes out twice.
  const preload = scope.registration.navigationPreload?.enable() ?? Promise.resolve();
  const cleaned = Promise.all([preload, deleteStaleCaches(keptCaches)]);
  event.waitUntil(cleaned.then(() => scope.clients.claim()));
});

scope.addEventListener('fetch', (event) => {
  const facts = requestFactsOf(event.request);
  const route = routeRequest(facts, scope.location.origin);
  if (route !== 'bypass') {
    event.respondWith(answerRoute(route, event, offlineCache));
  } else if (isPreloadedNavigation(facts)) {
    // Never stored: the paths the worker bypasses (account, admin, shares) stay uncached.
    event.respondWith(networkResponse(event));
  }
});
