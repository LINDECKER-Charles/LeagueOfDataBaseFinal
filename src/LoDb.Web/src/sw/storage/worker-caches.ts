import type { CacheSpec } from './cache-spec';

// Every cache of the worker starts with this prefix, and those of the previous site's worker
// too (`lodb-v3-…`): activation deletes any such cache that is not listed here.
export const CACHE_PREFIX = 'lodb-';

/**
 * The caches of the worker. Pages are few and refreshed on every visit; a page of the site
 * loads a few dozen hashed chunks; blobs are small images. Raising a version drops that cache.
 */
export const WORKER_CACHES = {
  pages: { name: `${CACHE_PREFIX}pages-v1`, limit: 40 },
  assets: { name: `${CACHE_PREFIX}assets-v1`, limit: 200 },
  blobs: { name: `${CACHE_PREFIX}blobs-v1`, limit: 400 },
} as const satisfies Record<string, CacheSpec>;
