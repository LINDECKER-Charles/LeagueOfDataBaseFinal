import { Component, PLATFORM_ID, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { CardValues } from '../facets/model/card-values';
import type { CatalogueListSource } from '../source/catalogue-list-source';
import type { ListStatus } from '../source/list-status';
import type { CatalogueCardAdapter } from '../state/catalogue-card-adapter';
import type { CatalogueListLike } from '../state/catalogue-list-like';
import { facetOf } from '../testing/facet-of';
import { CatalogueCardTemplate } from './catalogue-card-template';
import { CatalogueList } from './catalogue-list';

interface Item {
  readonly name: string;
  readonly tags: string[];
}

type List = CatalogueListLike<Item>;

const ITEMS: Item[] = [
  { name: 'Boots', tags: ['Boots'] },
  { name: 'Épée longue', tags: ['Damage'] },
  { name: 'Dagger', tags: ['Damage'] },
  { name: 'Ward', tags: ['Vision'] },
];
const WHOLE: List = { entries: ITEMS, total: ITEMS.length };
const FIRST: List = { entries: ITEMS.slice(0, 2), total: ITEMS.length };
const SCHEMA = [facetOf({ key: 'tag', kind: 'choice', label: 'Tag', primary: true })];
const ADAPTER: CatalogueCardAdapter<Item> = {
  searchTextOf: (item) => item.name,
  valuesOf: (item): CardValues => ({ tag: item.tags }),
  keyOf: (item) => item.name,
};
const WINDOW_MS = 300;

function sourceOf(dataset: List | null, status: ListStatus = 'ready') {
  const whole = signal<List | null>(dataset);
  const state = signal<ListStatus>(status);
  const source: CatalogueListSource<List> = {
    firstPage: signal(FIRST),
    slice: { page: 1, size: 2 },
    defaultSize: 2,
    dataset: whole,
    list: signal(dataset ?? FIRST),
    status: state,
  };
  return { source, whole, state };
}

let current = sourceOf(null).source;

@Component({
  imports: [CatalogueList, CatalogueCardTemplate],
  template: `<lodb-catalogue-list
    [source]="source"
    [adapter]="adapter"
    [schema]="schema"
    searchLabel="Search"
    label="Items"
  >
    <p class="card" *lodbCatalogueCard="let item of source">{{ item.name }}</p>
  </lodb-catalogue-list>`,
})
class Host {
  readonly source = current;
  readonly adapter = ADAPTER;
  readonly schema = SCHEMA;
}

describe('lodb-catalogue-list', () => {
  async function render(url: string, source: CatalogueListSource<List>) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        { provide: PLATFORM_ID, useValue: 'browser' },
        provideTransloco({
          config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
          loader: class {
            getTranslation = () => of({});
          },
        }),
      ],
    });
    await TestBed.inject(Router).navigateByUrl(url);
    current = source;
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    return fixture;
  }

  const all = (fixture: ComponentFixture<Host>, selector: string): HTMLElement[] =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll(selector));
  const names = (fixture: ComponentFixture<Host>) =>
    all(fixture, '.card').map((card) => card.textContent?.trim());

  async function type(fixture: ComponentFixture<Host>, text: string) {
    const field = all(fixture, 'input[type=search]')[0] as HTMLInputElement;
    field.value = text;
    field.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  it('draws the page the server rendered until the whole list arrives', async () => {
    const { source, whole } = sourceOf(null);
    const fixture = await render('/en/items', source);
    expect(names(fixture)).toEqual(['Boots', 'Épée longue']);
    whole.set(WHOLE);
    await fixture.whenStable();
    expect(names(fixture)).toEqual(['Boots', 'Épée longue']);
    expect(all(fixture, '.toolbar__pager a')).toHaveLength(1);
  });

  it('shows skeleton tiles for a filtered URL before the whole list', async () => {
    const fixture = await render('/en/items?q=dag', sourceOf(null).source);
    expect(names(fixture)).toEqual([]);
    expect(all(fixture, '[aria-busy=true] lodb-skeleton')).toHaveLength(2);
  });

  it('filters as the reader types, accents aside, and keeps it in the URL', async () => {
    const fixture = await render('/en/items?lang=en_GB', sourceOf(WHOLE).source);
    await type(fixture, 'EPEE');
    expect(names(fixture)).toEqual(['Épée longue']);
    await new Promise((resolve) => setTimeout(resolve, WINDOW_MS + 50));
    expect(TestBed.inject(Router).url).toBe('/en/items?lang=en_GB&q=EPEE');
  });

  it('narrows by a chip, lists it as active, and lets everything go', async () => {
    const fixture = await render('/en/items', sourceOf(WHOLE).source);
    const damage = all(fixture, 'lodb-filter-console .chip').find((chip) =>
      chip.textContent?.includes('Damage'),
    );
    damage?.click();
    await fixture.whenStable();
    expect(names(fixture)).toEqual(['Épée longue', 'Dagger']);
    expect(damage?.getAttribute('aria-pressed')).toBe('true');
    const active = all(fixture, '.active__chip span:first-child');
    expect(active.map((chip) => chip.textContent)).toEqual(['Tag: Damage']);
    all(fixture, 'lodb-active-filters .clear')[0].click();
    await fixture.whenStable();
    expect(names(fixture)).toEqual(['Boots', 'Épée longue']);
    expect(all(fixture, 'lodb-active-filters')).toHaveLength(0);
  });

  it('says so when nothing matches, and offers to clear', async () => {
    const fixture = await render('/en/items', sourceOf(WHOLE).source);
    await type(fixture, 'nothing like it');
    expect(all(fixture, '.empty')).toHaveLength(1);
    all(fixture, '.empty button')[0].click();
    await fixture.whenStable();
    expect(names(fixture)).toHaveLength(2);
  });

  it('turns pages in place on a plain click, through real links', async () => {
    const fixture = await render('/en/items', sourceOf(WHOLE).source);
    const next = all(fixture, '.toolbar__pager a')[0];
    expect(next.getAttribute('href')).toBe('/en/items?page=2');
    next.click();
    await fixture.whenStable();
    expect(names(fixture)).toEqual(['Dagger', 'Ward']);
  });

  it('warns while the version is being prepared', async () => {
    const fixture = await render('/en/items', sourceOf(null, 'pending').source);
    expect(all(fixture, '.notice[role=status]')).toHaveLength(1);
    expect(names(fixture)).toEqual(['Boots', 'Épée longue']);
  });

  it('jumps to the laid-out search on `/`, not while typing elsewhere', async () => {
    const fixture = await render('/en/items', sourceOf(WHOLE).source);
    const [rail] = all(fixture, 'lodb-filter-console input[type=search]');
    vi.spyOn(rail, 'getClientRects').mockReturnValue([new DOMRect()] as unknown as DOMRectList);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: '/' }));
    expect(document.activeElement).toBe(rail);
    rail.blur();
  });

  it('opens the facets in a bottom sheet on narrow screens', async () => {
    const fixture = await render('/en/items', sourceOf(WHOLE).source);
    all(fixture, '.bar__trigger')[0].click();
    await fixture.whenStable();
    const sheet = document.querySelector('lodb-filter-sheet');
    expect(sheet?.querySelectorAll('.chip')).toHaveLength(3);
  });
});
