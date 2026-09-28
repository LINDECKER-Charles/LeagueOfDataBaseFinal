import type { FavoriteSlot } from '../../../../core/api/generated/models/favorite-slot';
import type { FavoriteChoice } from './favorite-choice';
import type { FavoriteChoices } from './favorite-choices';

const EMPTY: FavoriteChoice = { id: null, name: null, image: null };

/**
 * The favorites once a save has come back: the slots it rejected are empty, as stored now,
 * unless they were picked again meanwhile, and the next save carries that newer pick.
 */
export function withoutRejected(
  current: FavoriteChoices,
  sent: FavoriteChoices,
  rejected: readonly FavoriteSlot[],
): FavoriteChoices {
  const emptied = rejected.filter((slot) => current[slot].id === sent[slot].id);
  if (emptied.length === 0) {
    return current;
  }
  return { ...current, ...Object.fromEntries(emptied.map((slot) => [slot, EMPTY])) };
}
