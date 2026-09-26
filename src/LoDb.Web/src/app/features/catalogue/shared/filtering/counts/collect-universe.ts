import type { FacetValue } from '../../facets/model/facet-value';
import type { FilterableCard } from '../grid/filterable-card';
import type { FacetUniverse } from './facet-universe';
import type { RangeBounds } from './range-bounds';

interface Universe {
  readonly present: Record<string, Set<string>>;
  readonly bounds: Record<string, RangeBounds>;
  readonly flagged: Record<string, number>;
}

function widened(bounds: RangeBounds | undefined, value: number): RangeBounds {
  return bounds === undefined
    ? { min: value, max: value }
    : { min: Math.min(bounds.min, value), max: Math.max(bounds.max, value) };
}

function collect(universe: Universe, key: string, value: FacetValue): void {
  if (Array.isArray(value)) {
    const tokens: readonly string[] = value;
    const present = (universe.present[key] ??= new Set<string>());
    tokens.forEach((token) => present.add(token));
  } else if (value === true) {
    universe.flagged[key] = (universe.flagged[key] ?? 0) + 1;
  } else if (typeof value === 'number') {
    universe.bounds[key] = widened(universe.bounds[key], value);
  }
}

/** Reads what the cards carry: the tokens of each choice, the span of each range, the flags. */
export function collectUniverse(cards: readonly FilterableCard[]): FacetUniverse {
  const universe: Universe = { present: {}, bounds: {}, flagged: {} };
  for (const card of cards) {
    for (const [key, value] of Object.entries(card.values)) {
      collect(universe, key, value);
    }
  }
  return universe;
}
