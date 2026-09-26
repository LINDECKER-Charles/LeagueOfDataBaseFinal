import { CACHE_PREFIX } from './worker-caches';

/** Deletes the caches of the site's workers, current or previous, that `kept` does not name. */
export async function deleteStaleCaches(kept: readonly string[]): Promise<void> {
  const names = await caches.keys();
  const stale = names.filter((name) => name.startsWith(CACHE_PREFIX) && !kept.includes(name));
  await Promise.all(stale.map((name) => caches.delete(name)));
}
