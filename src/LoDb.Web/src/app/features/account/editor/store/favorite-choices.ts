import type { FavoriteSlot } from '../../../../core/api/generated/models/favorite-slot';
import type { FavoriteChoice } from './favorite-choice';

/** The four favorite slots of the editor. */
export type FavoriteChoices = Readonly<Record<FavoriteSlot, FavoriteChoice>>;
