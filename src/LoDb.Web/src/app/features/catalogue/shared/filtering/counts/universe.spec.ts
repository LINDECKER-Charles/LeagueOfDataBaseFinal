import { facetOf } from '../../testing/facet-of';
import { choiceRowsOf } from './choice-rows-of';
import { collectUniverse } from './collect-universe';
import { offeredFacets } from './offered-facets';

const TAG = facetOf({
  key: 'tag',
  kind: 'choice',
  options: [
    { value: 'Boots', label: 'Bottes' },
    { value: 'Damage', label: 'Dégâts' },
    { value: 'Vision', label: 'Vision' },
  ],
});
const SCHEMA = [
  TAG,
  facetOf({ key: 'price', kind: 'range' }),
  facetOf({ key: 'level', kind: 'range' }),
  facetOf({ key: 'consumable', kind: 'toggle' }),
  facetOf({ key: 'edition', kind: 'choice' }),
];

const CARDS = [
  { card: 1, search: 'boots', values: { tag: ['Boots'], price: 300, level: 1 } },
  { card: 2, search: 'dagger', values: { tag: ['Damage', 'Mythic'], price: 250, level: 1 } },
];

describe('collectUniverse and offeredFacets', () => {
  const universe = collectUniverse(CARDS);

  it('reads the tokens, the spans and the flags the cards carry', () => {
    expect([...universe.present['tag']]).toEqual(['Boots', 'Damage', 'Mythic']);
    expect(universe.bounds['price']).toEqual({ min: 250, max: 300 });
    expect(universe.flagged['consumable']).toBeUndefined();
  });

  it('offers only what can narrow the list', () => {
    // A flag no card raises, a range whose values never differ, a choice nobody carries.
    expect(offeredFacets(SCHEMA, universe).map((facet) => facet.key)).toEqual(['tag', 'price']);
  });
});

describe('choiceRowsOf', () => {
  it('orders known values as the schema does, then unknown tokens, counted', () => {
    const rows = choiceRowsOf({
      facet: TAG,
      present: new Set(['Mythic', 'Damage', 'Boots']),
      selected: ['Damage'],
      counts: new Map([
        ['Boots', 0],
        ['Damage', 0],
        ['Mythic', 4],
      ]),
    });
    expect(rows).toEqual([
      { value: 'Boots', label: 'Bottes', isOn: false, count: 0, isDisabled: true },
      { value: 'Damage', label: 'Dégâts', isOn: true, count: 0, isDisabled: false },
      { value: 'Mythic', label: 'Mythic', isOn: false, count: 4, isDisabled: false },
    ]);
  });
});
