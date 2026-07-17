/**
 * LoDB service worker — offline resilience for a server-rendered MPA.
 *
 * Strategies:
 *  - Pages (real navigations AND Turbo Drive fetches, which are NOT
 *    mode:"navigate"): network-first with a timeout, falling back to the last
 *    cached copy, then to /offline.html. Never cache-first — the server-side
 *    analytics counts views at kernel.terminate.
 *  - /build (Vite-hashed) + /fonts: cache-first, immutable by construction.
 *  - /cdn/blobs (sha256-addressed MinIO): cache-first with a FIFO cap.
 *  - Data Dragon art (splash/centered): CORS re-fetch so responses are not
 *    opaque (opaque entries are quota-padded to ~7 MB each), small FIFO cap.
 *  - Bypassed entirely: non-GET, Range requests, SSE (text/event-stream — the
 *    loader stream must never cross a SW), /api, /admin, profiler, and the
 *    ability-video CDN (range streaming).
 */
const VERSION = 'lodb-v1';
const PAGES = `${VERSION}-pages`;
const ASSETS = `${VERSION}-assets`;
const BLOBS = `${VERSION}-blobs`;
const ART = `${VERSION}-art`;

const OFFLINE_URL = '/offline.html';
const PAGE_TIMEOUT_MS = 4000;
const PAGE_MAX = 40;
const ASSET_MAX = 80;
const BLOB_MAX = 400;
const ART_MAX = 80;

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(PAGES)
      .then((cache) => cache.add(OFFLINE_URL))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil((async () => {
    if (self.registration.navigationPreload) {
      await self.registration.navigationPreload.enable();
    }
    const keep = [PAGES, ASSETS, BLOBS, ART];
    for (const key of await caches.keys()) {
      if (!keep.includes(key)) {
        await caches.delete(key);
      }
    }
    await self.clients.claim();
  })());
});

self.addEventListener('fetch', (event) => {
  const request = event.request;
  if (request.method !== 'GET' || request.headers.has('range')) return;

  const accept = request.headers.get('accept') || '';
  if (accept.includes('text/event-stream')) return; // SSE: native handling only

  const url = new URL(request.url);
  const sameOrigin = url.origin === self.location.origin;

  if (sameOrigin && (
    url.pathname.startsWith('/api/')
    || url.pathname.startsWith('/admin')
    || url.pathname.startsWith('/_')
  )) return;

  // Turbo Drive visits are plain fetches for text/html — treat them as pages.
  const isPage = request.mode === 'navigate' || (sameOrigin && accept.includes('text/html'));
  if (isPage) {
    event.respondWith(pageStrategy(event));
    return;
  }

  if (sameOrigin && (url.pathname.startsWith('/build/') || url.pathname.startsWith('/fonts/'))) {
    event.respondWith(cacheFirst(ASSETS, request, ASSET_MAX));
    return;
  }
  if (sameOrigin && url.pathname.startsWith('/cdn/blobs/')) {
    event.respondWith(cacheFirst(BLOBS, request, BLOB_MAX));
    return;
  }
  if (url.hostname === 'ddragon.leagueoflegends.com') {
    event.respondWith(corsArtCache(request));
    return;
  }
  // Everything else (CommunityDragon, video CDN…): untouched.
});

async function pageStrategy(event) {
  const cache = await caches.open(PAGES);
  try {
    const response = await networkWithTimeout(event);
    if (response.ok) {
      cache.put(event.request, response.clone()).then(() => trim(cache, PAGE_MAX));
    }
    return response;
  } catch {
    const cached = await cache.match(event.request);
    return cached || cache.match(OFFLINE_URL);
  }
}

async function networkWithTimeout(event) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), PAGE_TIMEOUT_MS);
  try {
    const preloaded = await event.preloadResponse;
    if (preloaded) return preloaded;
    return await fetch(event.request, { signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}

async function cacheFirst(name, request, max) {
  const cache = await caches.open(name);
  const hit = await cache.match(request);
  if (hit) return hit;
  const response = await fetch(request);
  if (response.ok) {
    cache.put(request, response.clone()).then(() => trim(cache, max));
  }
  return response;
}

/** Re-fetch DDragon art in CORS mode (ACAO:* upstream) so cached responses are
    inspectable and quota-honest; on any failure fall back to the raw request. */
async function corsArtCache(request) {
  const cache = await caches.open(ART);
  const hit = await cache.match(request.url);
  if (hit) return hit;
  try {
    const response = await fetch(request.url, { mode: 'cors' });
    if (response.ok) {
      cache.put(request.url, response.clone()).then(() => trim(cache, ART_MAX));
    }
    return response;
  } catch {
    return fetch(request);
  }
}

/** FIFO trim — acceptable for immutable, content-addressed entries. */
async function trim(cache, max) {
  const keys = await cache.keys();
  for (let i = 0; i < keys.length - max; i++) {
    await cache.delete(keys[i]);
  }
}
