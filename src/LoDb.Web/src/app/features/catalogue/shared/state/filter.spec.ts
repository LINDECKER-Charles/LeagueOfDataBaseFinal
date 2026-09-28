import { PLATFORM_ID, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import type { CardValues } from '../facets/model/card-values';
import { PAGE_SIZE_ALL } from '../filtering/grid/page-size-all';
import type { CatalogueListSource } from '../source/catalogue-list-source';
import { facetOf } from '../testing/facet-of';
import { FilterUrlSync } from '../url/sync/filter-url-sync';
import type { CatalogueCardAdapter } from './catalogue-card-adapter';
import { CatalogueFilter } from './catalogue-filter';
import type { CatalogueListLike } from './catalogue-list-like';
import { filterableCardsOf } from './filterable-cards-of';
import { gridViewOf } from './grid-view-of';

interface Item {
  readonly name: string;
  readonly tags: string[];
  readonly price: number;
}

type List = CatalogueListLike<Item>;

const TAG = facetOf({ key: 'tag', kind: 'choice' });
const PRICE = facetOf({ key: 'price', kind: 'range' });
const SCHEMA = [TAG, PRICE];
const ADAPTER: CatalogueCardAdapter<Item> = {
  searchTextOf: (item) => item.name,
  valuesOf: (item): CardValues => ({ tag: item.tags, price: item.price }),
  keyOf: (item) => item.name,
};
const ITEMS: Item[] = [
  { name: 'Boots', tags: ['Boots'], price: 300 },
  { name: 'Épée longue', tags: ['Damage'], price: 350 },
  { name: 'Dagger', tags: ['Damage'], price: 250 },
  { name: 'Ward', tags: ['Vision'], price: 0 },
];
const WHOLE: List = { entries: ITEMS, total: ITEMS.length };
const FIRST: List = { entries: ITEMS.slice(0, 2), total: ITEMS.length };
const SLICE = { page: 1, size: 2 };
const WINDOW_MS = 300;

describe('gridViewOf', () => {
  const blank = { query: '', facets: {}, page: 1, size: 2 };
  const cards = filterableCardsOf(ITEMS, ADAPTER);

  it('shows the server page as it is while the state asks for exactly that page', () => {
    const view = gridViewOf({ cards: null, first: FIRST, slice: SLICE, state: blank, schema: [] });
    expect(view).toEqual({
      visible: FIRST.entries,
      matching: 4,
      total: 4,
      page: 1,
      pageCount: 2,
      isSkeleton: false,
    });
  });

  it('waits for the whole list before drawing a filtered or another page', () => {
    const filtered = { ...blank, query: 'boots' };
    const other = { ...blank, page: 2 };
    for (const state of [filtered, other]) {
      const view = gridViewOf({ cards: null, first: FIRST, slice: SLICE, state, schema: [] });
      expect(view.isSkeleton).toBe(true);
      expect(view.visible).toEqual([]);
    }
  });

  it('filters the whole list once known, without accents in the search', () => {
    const state = { ...blank, query: 'epee', size: PAGE_SIZE_ALL };
    const view = gridViewOf({ cards, first: FIRST, slice: SLICE, state, schema: SCHEMA });
    expect(view.visible.map((item) => item.name)).toEqual(['Épée longue']);
    expect(view).toMatchObject({ matching: 1, total: 4, page: 1, pageCount: 1 });
  });
});

describe('CatalogueFilter', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  async function filterAt(url: string) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        { provide: PLATFORM_ID, useValue: 'browser' },
        FilterUrlSync,
        CatalogueFilter,
      ],
    });
    const router = TestBed.inject(Router);
    await router.navigateByUrl(url);
    const dataset = signal<List | null>(null);
    const source: CatalogueListSource<List> = {
      firstPage: signal(FIRST),
      slice: SLICE,
      defaultSize: 2,
      dataset,
      list: signal(WHOLE),
      status: signal('ready'),
    };
    const filter = TestBed.inject(CatalogueFilter);
    filter.connect({ source: () => source, adapter: () => ADAPTER, schema: () => SCHEMA });
    return { filter, router, dataset };
  }

  it('starts from the URL it was opened with', async () => {
    const { filter } = await filterAt('/en/items?q=dag&tag=Damage&price=200-300&size=all');
    expect(filter.state()).toEqual({
      query: 'dag',
      facets: { tag: { values: ['Damage'], all: false }, price: { min: 200, max: 300 } },
      page: 1,
      size: PAGE_SIZE_ALL,
    });
    expect(filter.activeCount()).toBe(2);
    expect(filter.isEngaged()).toBe(true);
  });

  it('offers the facets of the server page first, then those of the whole list', async () => {
    const { filter, dataset } = await filterAt('/en/items');
    expect([...filter.universe().present['tag']]).toEqual(['Boots', 'Damage']);
    dataset.set(WHOLE);
    expect([...filter.universe().present['tag']]).toEqual(['Boots', 'Damage', 'Vision']);
    expect(filter.offered().map((facet) => facet.key)).toEqual(['tag', 'price']);
  });

  it('counts each value under the other engaged axes', async () => {
    const { filter, dataset } = await filterAt('/en/items?price=200-400');
    dataset.set(WHOLE);
    expect(Object.fromEntries(filter.counts().options['tag'])).toEqual({ Boots: 1, Damage: 2 });
  });

  it('sends the reader back to the first page when the filter narrows', async () => {
    const { filter, dataset } = await filterAt('/en/items?page=2');
    dataset.set(WHOLE);
    expect(filter.view().page).toBe(2);
    filter.toggleChoice(TAG, 'Damage');
    expect(filter.state().page).toBe(1);
    expect(filter.view().visible).toEqual([ITEMS[1], ITEMS[2]]);
  });

  it('writes each change to the URL after the window, foreign parameters kept', async () => {
    const { filter, router } = await filterAt('/en/items?lang=en_GB');
    filter.setQuery('b');
    filter.setQuery('boots');
    filter.setRange('price', { min: 100, max: 300 });
    await vi.advanceTimersByTimeAsync(WINDOW_MS);
    expect(router.url).toBe('/en/items?lang=en_GB&q=boots&price=100-300');
    filter.clearAll();
    await vi.advanceTimersByTimeAsync(WINDOW_MS);
    expect(router.url).toBe('/en/items?lang=en_GB');
  });

  it('clears one facet, switches a match mode, and keeps pages within bounds', async () => {
    const { filter, dataset } = await filterAt('/en/items?tag=Damage,Vision');
    dataset.set(WHOLE);
    filter.setMatchAll('tag', true);
    expect(filter.view().matching).toBe(0);
    filter.clearFacet('tag');
    expect(filter.activeCount()).toBe(0);
    filter.goTo(9);
    expect(filter.state().page).toBe(2);
    filter.setSize(PAGE_SIZE_ALL);
    expect(filter.state()).toMatchObject({ page: 1, size: PAGE_SIZE_ALL });
  });

  it('adopts the state of a navigation it did not ask for', async () => {
    const { filter, router } = await filterAt('/en/items?q=boots');
    await router.navigateByUrl('/en/items');
    expect(filter.state()).toEqual({ query: '', facets: {}, page: 1, size: 2 });
  });

  it('links another page of the list in its current state', async () => {
    const { filter } = await filterAt('/en/items?q=d');
    expect(filter.urlOf({ page: 2 })).toBe('/en/items?q=d&page=2');
    expect(filter.keyOf(ITEMS[0])).toBe('Boots');
  });
});
