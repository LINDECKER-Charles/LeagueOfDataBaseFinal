import { facetOf } from '../../testing/facet-of';
import type { FacetState } from '../model/facet-state';
import { activeFacetCount } from './active-facet-count';
import { countEngaged } from './count-engaged';
import { groupFacets } from './group-facets';
import { isGroupOpenByDefault } from './is-group-open-by-default';
import { matchesFacets } from './matches-facets';

const SCHEMA = [
  facetOf({ key: 'tag', kind: 'choice', matchAll: true }),
  facetOf({ key: 'edition', kind: 'choice', multiple: false }),
  facetOf({ key: 'price', kind: 'range' }),
  facetOf({ key: 'purchasable', kind: 'toggle' }),
];

const BOOTS = {
  tag: ['Boots', 'Armor'],
  edition: ['modern'],
  price: 300,
  purchasable: true as const,
};
const WARD = { tag: ['Vision'], edition: ['modern'], price: 0 };

describe('matchesFacets', () => {
  it('passes every card when nothing is engaged', () => {
    expect(matchesFacets(WARD, {}, SCHEMA)).toBe(true);
  });

  it('ORs the values of a choice, ANDs them in match-all mode', () => {
    const tags = (all: boolean, ...values: string[]): FacetState => ({ tag: { values, all } });
    expect(matchesFacets(BOOTS, tags(false, 'Boots', 'Vision'), SCHEMA)).toBe(true);
    expect(matchesFacets(BOOTS, tags(true, 'Boots', 'Vision'), SCHEMA)).toBe(false);
    expect(matchesFacets(BOOTS, tags(true, 'Boots', 'Armor'), SCHEMA)).toBe(true);
  });

  it('bounds a range inclusively and rejects cards without the value', () => {
    expect(matchesFacets(BOOTS, { price: { min: 300, max: 300 } }, SCHEMA)).toBe(true);
    expect(matchesFacets(BOOTS, { price: { min: 301, max: 900 } }, SCHEMA)).toBe(false);
    expect(matchesFacets({ tag: ['Boots'] }, { price: { min: 0, max: 900 } }, SCHEMA)).toBe(false);
  });

  it('keeps only flagged cards under a toggle', () => {
    expect(matchesFacets(BOOTS, { purchasable: true }, SCHEMA)).toBe(true);
    expect(matchesFacets(WARD, { purchasable: true }, SCHEMA)).toBe(false);
  });

  it('ANDs across facets', () => {
    const state: FacetState = {
      tag: { values: ['Boots'], all: false },
      price: { min: 0, max: 100 },
    };
    expect(matchesFacets(BOOTS, state, SCHEMA)).toBe(false);
  });

  it('ignores an engaged key the schema does not declare', () => {
    expect(matchesFacets(WARD, { unknown: true }, SCHEMA)).toBe(true);
  });

  it('counts engaged facets, whatever the number of chosen values', () => {
    const state: FacetState = {
      tag: { values: ['a', 'b'], all: false },
      price: { min: 0, max: 1 },
      purchasable: true,
    };
    expect(activeFacetCount(state)).toBe(3);
  });
});

describe('groupFacets', () => {
  const grouped = [
    facetOf({ key: 'role', kind: 'choice', group: 'Profile', primary: true }),
    facetOf({ key: 'hp', kind: 'range', group: 'Stats' }),
    facetOf({ key: 'range', kind: 'choice', group: 'Profile' }),
    facetOf({ key: 'armor', kind: 'range', group: 'Stats' }),
  ];

  it('keeps the schema order of groups and of facets within them', () => {
    const shape = groupFacets(grouped).map((group) => [
      group.name,
      group.facets.map((facet) => facet.key),
    ]);
    expect(shape).toEqual([
      ['Profile', ['role', 'range']],
      ['Stats', ['hp', 'armor']],
    ]);
  });

  it('counts the engaged facets of a group', () => {
    const [profile, stats] = groupFacets(grouped);
    const state: FacetState = {
      role: { values: ['Tank'], all: false },
      hp: { min: 500, max: 600 },
    };
    expect(countEngaged(profile.facets, state)).toBe(1);
    expect(countEngaged(stats.facets, state)).toBe(1);
    expect(countEngaged(stats.facets, {})).toBe(0);
  });

  it('unfolds a group by default for a primary facet or an engaged one', () => {
    const [profile, stats] = groupFacets(grouped);
    expect(isGroupOpenByDefault(profile, {})).toBe(true);
    expect(isGroupOpenByDefault(stats, {})).toBe(false);
    expect(isGroupOpenByDefault(stats, { armor: { min: 20, max: 40 } })).toBe(true);
  });
});
