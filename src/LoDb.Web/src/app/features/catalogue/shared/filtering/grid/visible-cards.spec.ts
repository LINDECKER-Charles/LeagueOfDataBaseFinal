import type { FacetState } from '../../facets/model/facet-state';
import { normalizeSearchText } from '../../search/normalize-search-text';
import { facetOf } from '../../testing/facet-of';
import type { FilterableCard } from './filterable-card';
import type { GridCriteria } from './grid-criteria';
import { isFilterEngaged } from './is-filter-engaged';
import { PAGE_SIZE_ALL } from './page-size-all';
import { selectVisibleCards } from './select-visible-cards';

const SCHEMA = [facetOf({ key: 'tag', kind: 'choice', primary: true })];

function card(search: string, tags: string[] = []): FilterableCard<string> {
  return { card: search, search, values: { tag: tags } };
}

const CARDS = [
  card('aatrox', ['Fighter']),
  card('ahri', ['Mage']),
  card('akali', ['Assassin', 'Fighter']),
  card('alistar', ['Tank']),
  card('amumu', ['Tank', 'Mage']),
];

function select(criteria: Partial<GridCriteria> = {}) {
  return selectVisibleCards(CARDS, {
    query: '',
    facets: {},
    schema: SCHEMA,
    page: 1,
    pageSize: PAGE_SIZE_ALL,
    ...criteria,
  });
}

const names = (cards: readonly FilterableCard<string>[]) => cards.map((entry) => entry.card);
const tags = (...values: string[]): FacetState => ({ tag: { values, all: false } });

describe('selectVisibleCards, matching', () => {
  it('matches the query case-insensitively and ignores padding', () => {
    expect(names(select({ query: '  AKA ' }).matching)).toEqual(['akali']);
  });

  it('folds accents so "feerique" finds "féérique"', () => {
    const folded = card(normalizeSearchText('charme féérique'));
    const result = selectVisibleCards([folded], {
      query: 'feerique',
      facets: {},
      schema: SCHEMA,
      page: 1,
      pageSize: PAGE_SIZE_ALL,
    });
    expect(result.matching).toHaveLength(1);
  });

  it('ORs the chosen values and ANDs them with the query', () => {
    expect(names(select({ facets: tags('Tank', 'Mage') }).matching)).toEqual([
      'ahri',
      'alistar',
      'amumu',
    ]);
    expect(names(select({ query: 'am', facets: tags('Tank') }).matching)).toEqual(['amumu']);
  });

  it('keeps every card when nothing is engaged', () => {
    expect(select().matching).toHaveLength(5);
  });
});

describe('selectVisibleCards, pagination', () => {
  it('slices the requested page', () => {
    const result = select({ page: 2, pageSize: 2 });
    expect(result.pageCount).toBe(3);
    expect(names(result.visible)).toEqual(['akali', 'alistar']);
  });

  it('shows everything on a single page under the ALL sentinel', () => {
    const result = select({ pageSize: PAGE_SIZE_ALL });
    expect(result.pageCount).toBe(1);
    expect(result.visible).toHaveLength(5);
  });

  it('never reports fewer than one page', () => {
    expect(select({ query: 'zzz', pageSize: 2 }).pageCount).toBe(1);
  });

  it('sends a stranded page back to the first one', () => {
    const result = select({ page: 3, pageSize: 2, facets: tags('Tank') });
    expect(result.page).toBe(1);
    expect(names(result.visible)).toEqual(['alistar', 'amumu']);
  });

  it('leaves an in-range page untouched', () => {
    expect(select({ page: 2, pageSize: 2 }).page).toBe(2);
  });
});

describe('isFilterEngaged', () => {
  it('ignores a blank search and counts a facet', () => {
    expect(isFilterEngaged('   ', {})).toBe(false);
    expect(isFilterEngaged('ahri', {})).toBe(true);
    expect(isFilterEngaged('', tags('Tank'))).toBe(true);
  });
});
