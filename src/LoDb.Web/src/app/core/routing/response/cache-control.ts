import type { CacheClass } from './cache-class';

/**
 * `Cache-Control` of each class of response (lot 3, L3.1). The latest version changes at
 * each patch only, and a deployment changes the page cache's key anyway: the proxy keeps it
 * thirty minutes, then serves it stale for a day while one request refreshes it, so a new
 * patch shows within half an hour. An older version never changes again: the proxy keeps it
 * a year, crawlers coming back to a page weeks later, and a browser an hour (ADR 0011,
 * amending 0005). The bundles its HTML names outlive it: the deploy job keeps them 400 days
 * (`LODB_FRONT_BUILDS_RETENTION_DAYS`, `_deploy.yml`). A redirect or an error may stop being
 * true at the next patch (a 301 from `/{latest}/…`, a 404 of a version not ingested yet), so
 * it lives a minute; a private page is never stored.
 */
export const CACHE_CONTROL: Readonly<Record<CacheClass, string>> = {
  latest: 'public, max-age=0, s-maxage=1800, stale-while-revalidate=86400',
  archived: 'public, max-age=3600, s-maxage=31536000',
  transient: 'public, max-age=0, s-maxage=60',
  private: 'private, no-store',
};
