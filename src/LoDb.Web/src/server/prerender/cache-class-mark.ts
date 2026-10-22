import type { CacheClass } from '../../app/core/routing/response/cache-class';
import { CACHE_CONTROL } from '../../app/core/routing/response/cache-control';
import type { RenderLane } from '../admission/render-lane';

const CACHE_CLASS_HEADER = 'X-LoDb-Cache-Class';
// The classes the pass stores: an older patch's page of a closed state, which never changes
// again, and one of the open state, rendered again when a promotion closes it. The store keeps
// the page's Cache-Control, which nginx follows.
const STORED_CLASSES: readonly CacheClass[] = ['archived', 'revisable'];

/**
 * The header a render of the pre-render pass gets when its page may be stored,
 * `X-LoDb-Cache-Class: archived` or `revisable`, from the `Cache-Control` the page chose
 * (`CACHE_CONTROL`): the API reads the mark rather than a copy of the class's value, which
 * would drift from it. None for any other lane or class (a redirect, a 404, the latest patch, a
 * private page).
 */
export function cacheClassMark(
  lane: RenderLane,
  cacheControl: string | null,
): Readonly<Record<string, string>> {
  if (lane !== 'prerender') {
    return {};
  }
  const stored = STORED_CLASSES.find((cacheClass) => CACHE_CONTROL[cacheClass] === cacheControl);
  return stored === undefined ? {} : { [CACHE_CLASS_HEADER]: stored };
}
