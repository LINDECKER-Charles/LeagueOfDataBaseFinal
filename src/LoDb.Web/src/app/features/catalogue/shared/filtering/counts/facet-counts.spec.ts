import type { CardValues } from '../../facets/model/card-values';
import type { FacetState } from '../../facets/model/facet-state';
import { facetOf } from '../../testing/facet-of';
import { countFacetOptions } from './count-facet-options';

const SCHEMA = [
  facetOf({ key: 'tag', kind: 'choice', matchAll: true }),
  facetOf({ key: 'edition', kind: 'choice', multiple: false }),
  facetOf({ key: 'price', kind: 'range' }),
  facetOf({ key: 'purchasable', kind: 'toggle' }),
];

interface Row {
  readonly tag: string[];
  readonly edition: string;
  readonly price: number;
  readonly purchasable?: boolean;
}

function card(search: string, { tag, edition, price, purchasable = false }: Row) {
  const flag: CardValues = purchasable ? { purchasable: true } : {};
  const values: CardValues = { tag, edition: [edition], price, ...flag };
  return { card: search, search, values };
}

const CARDS = [
  card('boots', { tag: ['Boots', 'Armor'], edition: 'modern', price: 300, purchasable: true }),
  card('boots classic', { tag: ['Boots'], edition: 'classic', price: 300, purchasable: true }),
  card('dagger', { tag: ['Damage'], edition: 'modern', price: 250 }),
  card('ward', { tag: ['Vision'], edition: 'modern', price: 0, purchasable: true }),
];

const count = (query: string, facets: FacetState) =>
  countFacetOptions(CARDS, { query, facets, schema: SCHEMA });

describe('countFacetOptions', () => {
  it('tallies every value of every card when nothing is engaged', () => {
    const counts = count('', {});
    expect([...counts.options['tag']]).toEqual([
      ['Boots', 2],
      ['Armor', 1],
      ['Damage', 1],
      ['Vision', 1],
    ]);
    expect([...counts.options['edition']]).toEqual([
      ['modern', 3],
      ['classic', 1],
    ]);
    expect(counts.flagged['purchasable']).toBe(3);
    expect(counts.options['price']).toBeUndefined();
  });

  it('counts a facet under the other axes but not under its own selection', () => {
    const counts = count('', { edition: { values: ['classic'], all: false } });
    // Tags narrowed to the classic card...
    expect([...counts.options['tag']]).toEqual([['Boots', 1]]);
    // ...while editions keep counting everything: "what you would get".
    expect([...counts.options['edition']]).toEqual([
      ['modern', 3],
      ['classic', 1],
    ]);
    expect(counts.flagged['purchasable']).toBe(1);
  });

  it('keeps the selection of a match-all choice, which can only narrow', () => {
    const counts = count('', { tag: { values: ['Boots'], all: true } });
    expect([...counts.options['tag']]).toEqual([
      ['Boots', 2],
      ['Armor', 1],
    ]);
    expect([...counts.options['edition']]).toEqual([
      ['modern', 1],
      ['classic', 1],
    ]);
  });

  it('applies the search and the ranges like every other axis', () => {
    expect([...count('boots', {}).options['edition']]).toEqual([
      ['modern', 1],
      ['classic', 1],
    ]);
    const counts = count('', { price: { min: 250, max: 300 } });
    expect([...counts.options['tag']]).toEqual([
      ['Boots', 2],
      ['Armor', 1],
      ['Damage', 1],
    ]);
    expect(counts.flagged['purchasable']).toBe(2);
  });
});
