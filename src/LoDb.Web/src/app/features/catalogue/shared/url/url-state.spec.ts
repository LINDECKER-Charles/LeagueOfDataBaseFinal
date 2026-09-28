import { PAGE_SIZE_ALL } from '../filtering/grid/page-size-all';
import { facetOf } from '../testing/facet-of';
import type { FilterUrlSpec } from './filter-url-spec';
import type { FilterUrlState } from './filter-url-state';
import { parseFilterUrl } from './parse-filter-url';
import { writeFilterUrl } from './write-filter-url';

const SPEC: FilterUrlSpec = {
  schema: [
    facetOf({ key: 'tag', kind: 'choice', matchAll: true }),
    facetOf({ key: 'edition', kind: 'choice', multiple: false }),
    facetOf({ key: 'price', kind: 'range' }),
    facetOf({ key: 'as', kind: 'range', step: 0.01 }),
    facetOf({ key: 'purchasable', kind: 'toggle' }),
  ],
  defaultSize: 12,
};

const STATE: FilterUrlState = {
  query: 'boots',
  facets: {
    tag: { values: ['Boots', 'Armor'], all: true },
    edition: { values: ['classic'], all: false },
    price: { min: 0, max: 3000 },
    purchasable: true,
  },
  page: 2,
  size: 24,
};

const blank = (facets: FilterUrlState['facets'] = {}): FilterUrlState => ({
  query: '',
  facets,
  page: 1,
  size: SPEC.defaultSize,
});

describe('writeFilterUrl', () => {
  it('writes a canonical query: schema order, sorted values, explicit bounds', () => {
    expect(writeFilterUrl('', STATE, SPEC)).toBe(
      '?q=boots&tag=Armor%2CBoots&tag_all=1&edition=classic&price=0-3000&purchasable=1' +
        '&page=2&size=24',
    );
  });

  it('omits the defaults and the empty query', () => {
    expect(writeFilterUrl('', { ...blank(), query: '  ' }, SPEC)).toBe('');
  });

  it('carries the foreign parameters through, byte for byte, ahead of its own', () => {
    const state = { ...blank({ purchasable: true }), size: PAGE_SIZE_ALL };
    expect(writeFilterUrl('?lang=fr_FR&tag=Old&utm=a%20b&version=16.1.1', state, SPEC)).toBe(
      '?lang=fr_FR&utm=a%20b&version=16.1.1&purchasable=1&size=all',
    );
  });

  it('writes the same URL whatever the order the values were chosen in', () => {
    const a = writeFilterUrl('', blank({ tag: { values: ['Boots', 'Armor'], all: false } }), SPEC);
    const b = writeFilterUrl('', blank({ tag: { values: ['Armor', 'Boots'], all: false } }), SPEC);
    expect(a).toBe(b);
  });

  it('keeps three decimals at most on range bounds', () => {
    expect(writeFilterUrl('', blank({ as: { min: 0.625, max: 0.8 } }), SPEC)).toBe('?as=0.625-0.8');
  });

  it('drops the match-all flag of a facet that does not offer it', () => {
    expect(writeFilterUrl('', blank({ edition: { values: ['classic'], all: true } }), SPEC)).toBe(
      '?edition=classic',
    );
  });
});

describe('parseFilterUrl', () => {
  it('round-trips the written state', () => {
    const url = writeFilterUrl('', STATE, SPEC);
    expect(parseFilterUrl(url, SPEC)).toEqual({
      ...STATE,
      facets: { ...STATE.facets, tag: { values: ['Armor', 'Boots'], all: true } },
    });
  });

  it('reads the separators the router leaves unescaped', () => {
    expect(parseFilterUrl('?tag=Armor,Boots&q=kai%27sa', SPEC)).toMatchObject({
      query: "kai'sa",
      facets: { tag: { values: ['Armor', 'Boots'], all: false } },
    });
  });

  it('falls back to the defaults on absent or malformed parameters', () => {
    expect(parseFilterUrl('?page=zero&size=-3&price=abc&purchasable=yes', SPEC)).toEqual(blank());
  });

  it('drops an inverted range and keeps one value of a single choice', () => {
    const parsed = parseFilterUrl('?price=900-100&edition=classic,modern', SPEC);
    expect(parsed.facets).toEqual({ edition: { values: ['classic'], all: false } });
  });

  it('ignores the match-all flag on a facet that does not offer it', () => {
    const parsed = parseFilterUrl('?edition=classic&edition_all=1', SPEC);
    expect(parsed.facets['edition']).toEqual({ values: ['classic'], all: false });
  });

  it('reads the ALL page size', () => {
    expect(parseFilterUrl('?size=all', SPEC).size).toBe(PAGE_SIZE_ALL);
  });

  it('reads the page and the size without any schema', () => {
    expect(parseFilterUrl('?tag=Boots&page=3&size=48', { schema: [], defaultSize: 12 })).toEqual({
      query: '',
      facets: {},
      page: 3,
      size: 48,
    });
  });
});
