import type { CardValues } from '../../facets/model/card-values';

/** A card of the list as the filter reads it: the card itself, its haystack and its values. */
export interface FilterableCard<T = unknown> {
  readonly card: T;
  /** What the search compares against, already normalized (normalizeSearchText). */
  readonly search: string;
  readonly values: CardValues;
}
