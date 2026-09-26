import type { SaveFavoritesRequest } from '../../../../core/api/generated/models/save-favorites-request';
import type { FavoriteChoices } from './favorite-choices';
import type { SkinChoice } from './skin-choice';

/** The body of a favorites save: every slot stated, an empty one as null. */
export function favoritesBody(
  choices: FavoriteChoices,
  skin: SkinChoice | null,
): SaveFavoritesRequest {
  return {
    champion: choices.champion.id,
    item: choices.item.id,
    rune: choices.rune.id,
    summoner: choices.summoner.id,
    skin: skin?.id ?? null,
  };
}
