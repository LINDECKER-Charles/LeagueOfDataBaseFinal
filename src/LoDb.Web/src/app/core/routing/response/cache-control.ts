import type { CacheClass } from './cache-class';

/**
 * `Cache-Control` of each class of response (lot 3, L3.1). The latest version changes at
 * each patch, so the proxy keeps it five minutes and serves it stale while it refreshes; an
 * older version never changes again; a redirect or an error may stop being true at the next
 * patch (a 301 from `/{latest}/…`, a 404 of a version not ingested yet), so it lives a
 * minute; a private page is never stored.
 */
export const CACHE_CONTROL: Readonly<Record<CacheClass, string>> = {
  latest: 'public, max-age=0, s-maxage=300, stale-while-revalidate=3600',
  archived: 'public, max-age=3600, s-maxage=604800',
  transient: 'public, max-age=0, s-maxage=60',
  private: 'private, no-store',
};
