import { PrerenderFallback, RenderMode, type ServerRoute } from '@angular/ssr';
import { LOCALES } from './core/i18n/locales';
import { CACHE_CONTROL } from './core/routing/response/cache-control';
import { NOINDEX_HEADERS } from './core/routing/response/noindex-headers';

// The pages of EDITORIAL_ROUTES, prerendered for the 21 locales (ADR 0005).
const PRERENDERED_PATHS = [
  'about',
  'about/data',
  'faq',
  'changelog',
  'legal/notice',
  'legal/privacy',
  'legal/terms',
  'legal/cookies',
];

const PUBLIC_HEADERS = { 'Cache-Control': CACHE_CONTROL.latest };
const PRIVATE_HEADERS = { 'Cache-Control': CACHE_CONTROL.private, ...NOINDEX_HEADERS };

// An unknown locale was not prerendered: its URL renders per request, which answers the 404.
function prerendered(path: string): ServerRoute {
  return {
    path: `:locale/${path}`,
    renderMode: RenderMode.Prerender,
    getPrerenderParams: async () => LOCALES.map((locale) => ({ locale })),
    fallback: PrerenderFallback.Server,
    headers: PUBLIC_HEADERS,
  };
}

/**
 * Render mode and headers of each page (ADR 0005). Public pages render on the server,
 * anonymous, with the cache of the latest version: their resolvers replace it for an older
 * pinned version, a redirect or an error (PageResponse). Private pages render in the browser
 * only, never stored, never indexed; a shared build renders on the server, never indexed.
 */
export const serverRoutes: ServerRoute[] = [
  ...PRERENDERED_PATHS.map(prerendered),
  { path: ':locale/account/**', renderMode: RenderMode.Client, headers: PRIVATE_HEADERS },
  { path: 'admin/**', renderMode: RenderMode.Client, headers: PRIVATE_HEADERS },
  {
    path: 'b/:token',
    renderMode: RenderMode.Server,
    headers: { ...PUBLIC_HEADERS, ...NOINDEX_HEADERS },
  },
  { path: '**', renderMode: RenderMode.Server, headers: PUBLIC_HEADERS },
];
