import type { CacheSlot } from './cache-slot';
import type { CacheSpec } from './cache-spec';

/**
 * Opens a cache of the worker, or resolves undefined when storage is refused (private
 * browsing, quota): the strategies then simply work without a copy.
 */
export function openSlot(spec: CacheSpec): Promise<CacheSlot | undefined> {
  return caches.open(spec.name).then(
    (cache) => ({ cache, limit: spec.limit }),
    () => undefined,
  );
}
