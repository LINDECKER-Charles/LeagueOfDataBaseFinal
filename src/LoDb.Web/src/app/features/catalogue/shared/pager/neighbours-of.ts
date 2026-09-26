import type { Neighbours } from './neighbours';

const NONE: Neighbours<never> = { previous: null, next: null };

/**
 * The neighbours of an entry in its list's order, without wrapping round: the first entry
 * has no previous one. An entry the list does not hold has none.
 */
export function neighboursOf<C>(
  cards: readonly C[],
  key: string,
  keyOf: (card: C) => string,
): Neighbours<C> {
  const index = cards.findIndex((card) => keyOf(card) === key);
  if (index < 0) {
    return NONE;
  }
  return { previous: cards[index - 1] ?? null, next: cards[index + 1] ?? null };
}
