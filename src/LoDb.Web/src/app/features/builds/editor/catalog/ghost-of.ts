import type { ItemOption } from '../../../../core/api/generated/models/item-option';
import type { GhostReason } from './ghost-reason';

/**
 * Why a placed item is a ghost. Never judged while the current list is not read: an item
 * is no ghost until its absence is known.
 *
 * @param current the items of the current patch and mode, by id; null while not read
 * @param known every item a list of this visit held, by id
 */
export function ghostOf(
  itemId: string,
  current: ReadonlyMap<string, ItemOption> | null,
  known: ReadonlyMap<string, ItemOption>,
): GhostReason {
  if (current === null || current.has(itemId)) {
    return null;
  }
  return known.has(itemId) ? 'mode' : 'patch';
}
