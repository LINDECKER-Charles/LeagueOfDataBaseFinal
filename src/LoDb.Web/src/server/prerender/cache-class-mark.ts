import type { CacheClass } from '../../app/core/routing/response/cache-class';
import { CACHE_CONTROL } from '../../app/core/routing/response/cache-control';
import type { RenderLane } from '../admission/render-lane';

const CACHE_CLASS_HEADER = 'X-LoDb-Cache-Class';
// The only class the pass stores: an older patch's page never changes again.
const STORED_CLASS: CacheClass = 'archived';

/**
 * The header a render of the pre-render pass gets when its page may be stored,
 * `X-LoDb-Cache-Class: archived`, from the `Cache-Control` the page chose (`CACHE_CONTROL`):
 * the API reads the mark rather than a copy of the class's value, which would drift from it.
 * None for any other lane or class (a redirect, a 404, the latest patch, a private page).
 */
export function cacheClassMark(
  lane: RenderLane,
  cacheControl: string | null,
): Readonly<Record<string, string>> {
  const isStored = lane === 'prerender' && cacheControl === CACHE_CONTROL[STORED_CLASS];
  return isStored ? { [CACHE_CLASS_HEADER]: STORED_CLASS } : {};
}
