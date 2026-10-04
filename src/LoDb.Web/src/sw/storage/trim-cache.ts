/**
 * Deletes the oldest entries beyond `limit`. First in, first out is enough: pages are
 * refreshed on every visit, assets and blobs never change under their URL.
 */
export async function trimCache(cache: Cache, limit: number): Promise<void> {
  const keys = await cache.keys();
  const excess = keys.slice(0, Math.max(0, keys.length - limit));
  await Promise.all(excess.map((key) => cache.delete(key)));
}
