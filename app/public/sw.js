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
 *    loader stream must never cross a SW), /api, /admin, profiler, the
 *    private/transactional surfaces (auth, profile, builds, donation, Stripe
 *    webhooks), and the ability-video CDN (range streaming).
 */
const VERSION = 'lodb-v2';
const PAGES = `${VERSION}-pages`;
const ASSETS = `${VERSION}-assets`;
const BLOBS = `${VERSION}-blobs`;
const ART = `${VERSION}-art`;

/** Same-origin prefixes never touched by the SW: APIs, admin, profiler, and the
    private/transactional pages (auth, profile, public profiles, builds, build
    shares, donation, webhooks). NB: '/b/' does not match '/build/' — the Vite
    assets stay on the cache-first path below. */
const BYPASS_PATH_PREFIXES = [
  '/api/', '/admin', '/_',
  '/login', '/register', '/logout',
  '/profile', '/u/', '/builds', '/b/',
  '/donate', '/webhooks',
];

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

  if (sameOrigin && BYPASS_PATH_PREFIXES.some((prefix) => url.pathname.startsWith(prefix))) return;

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
  const cache = await caches.open(PAGES).catch(() => null);
  const cached = cache ? await cache.match(event.request) : undefined;
  try {
    // Race the network against a timeout ONLY when a cached page can take over;
    // with no fallback, aborting a slow-but-valid first visit just turns it into a
    // broken navigation (net::ERR_FAILED). Let the network run to completion then.
    const response = await networkFetch(event, cached ? PAGE_TIMEOUT_MS : null);
    if (cache && response.ok) {
      cache.put(event.request, response.clone()).then(() => trim(cache, PAGE_MAX));
    }
    return response;
  } catch {
    // Guarantee a Response on every path — respondWith(undefined) throws
    // "Failed to convert value to 'Response'" and breaks the navigation.
    return cached || (cache && await cache.match(OFFLINE_URL)) || offlineResponse();
  }
}

async function networkFetch(event, timeoutMs) {
  const preloaded = await event.preloadResponse;
  if (preloaded) return preloaded;
  if (timeoutMs === null) return fetch(event.request);

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(event.request, { signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}

/** Last-resort page body when even the cached offline shell is unavailable. */
function offlineResponse() {
  return new Response(
    '<!doctype html><meta charset="utf-8"><title>Hors ligne</title><p>Contenu indisponible hors ligne.</p>',
    { status: 503, headers: { 'Content-Type': 'text/html; charset=utf-8' } },
  );
}

async function cacheFirst(name, request, max) {
  const cache = await caches.open(name).catch(() => null);
  const hit = cache ? await cache.match(request) : undefined;
  if (hit) return hit;
  try {
    const response = await fetch(request);
    if (cache && response.ok) {
      cache.put(request, response.clone()).then(() => trim(cache, max));
    }
    return response;
  } catch {
    // Nothing cached and the network is down — fail the sub-resource cleanly
    // rather than rejecting respondWith (unhandled "network error" promise).
    return Response.error();
  }
}

/** Re-fetch DDragon art in CORS mode (ACAO:* upstream) so cached responses are
    inspectable and quota-honest; on any failure fall back to the raw request. */
async function corsArtCache(request) {
  const cache = await caches.open(ART).catch(() => null);
  const hit = cache ? await cache.match(request.url) : undefined;
  if (hit) return hit;
  try {
    const response = await fetch(request.url, { mode: 'cors' });
    if (cache && response.ok) {
      cache.put(request.url, response.clone()).then(() => trim(cache, ART_MAX));
    }
    return response;
  } catch {
    // CORS re-fetch failed (offline / upstream): fall back to the raw request,
    // and if that fails too, fail cleanly instead of rejecting respondWith.
    return fetch(request).catch(() => Response.error());
  }
}

/** FIFO trim — acceptable for immutable, content-addressed entries. */
async function trim(cache, max) {
  const keys = await cache.keys();
  for (let i = 0; i < keys.length - max; i++) {
    await cache.delete(keys[i]);
  }
}
