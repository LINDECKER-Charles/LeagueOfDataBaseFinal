import type { FavoriteBoard } from '../../../../core/api/generated/models/favorite-board';
import type { FavoriteView } from '../../../../core/api/generated/models/favorite-view';
import { FAVORITE_SLOT } from '../../../../core/api/generated/models/favorite-slot-array';
import { imageSource } from '../entries/image-source';
import type { FavoriteChoice } from './favorite-choice';
import type { FavoriteChoices } from './favorite-choices';

function choiceOf(view: FavoriteView): FavoriteChoice {
  const current = view.current;
  return {
    // The stored id, never the resolved one: a favorite the patch lacks is saved back as is.
    id: view.storedId ?? current?.id ?? null,
    name: current?.name ?? null,
    image: current === null ? null : imageSource(current.image),
  };
}

/** The favorites of a profile as the editor holds them. */
export function favoriteChoicesOf(board: FavoriteBoard): FavoriteChoices {
  const entries = FAVORITE_SLOT.map((slot) => [slot, choiceOf(board[slot])] as const);
  return Object.fromEntries(entries) as FavoriteChoices;
}
