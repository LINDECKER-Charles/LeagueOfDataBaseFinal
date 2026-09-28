import { Component, PLATFORM_ID, type Type, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import {
  type Translation,
  TranslocoPipe,
  provideTransloco,
  provideTranslocoScope,
} from '@jsverse/transloco';
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
// A second group, folded by default: nothing in it is a main axis of the list.
const WITH_KIND = [
  ...SCHEMA,
  facetOf({ key: 'kind', kind: 'choice', label: 'Kind', group: 'More' }),
];
const ADAPTER: CatalogueCardAdapter<Item> = {
  searchTextOf: (item) => item.name,
  valuesOf: (item): CardValues => ({ tag: item.tags, kind: item.tags }),
  keyOf: (item) => item.name,
};
const WINDOW_MS = 300;
// The catalogue scope of a page that lists its own texts, such as `summoners`.
const SCOPED: Record<string, Translation> = {
  en: { common: { search: 'Search…' }, filter: { results: '{{ count }} results' } },
  'feature/en': { search: 'Search for a feature' },
};

function sourceOf(dataset: List | null, status: ListStatus = 'ready', first: List = FIRST) {
  const whole = signal<List | null>(dataset);
  const state = signal<ListStatus>(status);
  const source: CatalogueListSource<List> = {
    firstPage: signal(first),
    slice: { page: 1, size: 2 },
    defaultSize: 2,
    dataset: whole,
    list: signal(dataset ?? FIRST),
    status: state,
  };
  return { source, whole, state };
}

let current = sourceOf(null).source;
let currentSchema = SCHEMA;

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
  readonly schema = currentSchema;
}

// A page under its own catalogue scope, which translates the label it hands the list.
@Component({
  imports: [CatalogueList, CatalogueCardTemplate, TranslocoPipe],
  providers: [provideTranslocoScope('feature')],
  template: `<lodb-catalogue-list
    [source]="source"
    [adapter]="adapter"
    [searchLabel]="'feature.search' | transloco"
    label="Items"
  >
    <p class="card" *lodbCatalogueCard="let item of source">{{ item.name }}</p>
  </lodb-catalogue-list>`,
})
class ScopedHost {
  readonly source = current;
  readonly adapter = ADAPTER;
}

describe('lodb-catalogue-list', () => {
  afterEach(() => {
    currentSchema = SCHEMA;
    vi.unstubAllGlobals();
  });

  async function render(
    url: string,
    source: CatalogueListSource<List>,
    host: Type<unknown> = Host,
  ) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        { provide: PLATFORM_ID, useValue: 'browser' },
        provideTransloco({
          config: {
            availableLangs: ['en'],
            defaultLang: 'en',
            missingHandler: { logMissingKey: false },
            prodMode: true,
          },
          loader: class {
            getTranslation = (path: string) => of(SCOPED[path] ?? {});
          },
        }),
      ],
    });
    await TestBed.inject(Router).navigateByUrl(url);
    current = source;
    const fixture = TestBed.createComponent(host);
    await fixture.whenStable();
    return fixture;
  }

  const all = (fixture: ComponentFixture<unknown>, selector: string): HTMLElement[] =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll(selector));
  const names = (fixture: ComponentFixture<unknown>) =>
    all(fixture, '.card').map((card) => card.textContent?.trim());

  async function type(fixture: ComponentFixture<unknown>, text: string) {
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

  it('translates the label a scoped page hands it within the scope of that page', async () => {
    const fixture = await render('/en/items', sourceOf(WHOLE).source, ScopedHost);

    const [search] = all(fixture, 'lodb-filter-console input[type=search]');
    expect(search?.getAttribute('aria-label')).toBe('Search for a feature');
    // The placeholder stays the short one of every list, which the narrow rail can hold.
    expect(search?.getAttribute('placeholder')).toBe('Search…');
  });

  it('keeps a group open when its last facet is cleared under the pointer', async () => {
    currentSchema = WITH_KIND;
    const fixture = await render('/en/items?kind=Vision', sourceOf(WHOLE).source);
    const heading = () =>
      all(fixture, 'lodb-filter-console lodb-facet-group button')
        .find((button) => button.textContent?.includes('More'))
        ?.getAttribute('aria-expanded');
    expect(heading()).toBe('true');
    all(fixture, '.active__chip')[0].click();
    await fixture.whenStable();
    expect(all(fixture, 'lodb-active-filters')).toHaveLength(0);
    expect(heading()).toBe('true');
  });

  it('offers the link of the list as filtered at the foot of the rail', async () => {
    vi.stubGlobal('navigator', {});
    const fixture = await render('/en/items?lang=en_GB&tag=Damage', sourceOf(WHOLE).source);
    all(fixture, 'lodb-filter-console .console__foot button')[0].click();
    await fixture.whenStable();
    const [field] = all(fixture, '.console__foot input[readonly]') as HTMLInputElement[];
    expect(field?.value).toBe(`${location.origin}/en/items?lang=en_GB&tag=Damage`);
  });

  it('sets the figure of the count apart from its words', async () => {
    const fixture = await render('/en/items', sourceOf(WHOLE).source);
    expect(all(fixture, '.toolbar__figure').map((node) => node.textContent)).toEqual(['4']);
    expect(all(fixture, '.toolbar__text').map((node) => node.textContent)).toEqual(['', 'results']);
  });

  it('frames the empty list of a version that holds nothing, with a way home', async () => {
    const empty: List = { entries: [], total: 0 };
    const fixture = await render('/en/7.20.1/runes', sourceOf(empty, 'ready', empty).source);
    expect(all(fixture, '[role=list]')).toHaveLength(0);
    expect(all(fixture, 'lodb-empty-state a')[0]?.getAttribute('href')).toBe('/en');
  });

  it('opens the facets in a bottom sheet on narrow screens', async () => {
    const fixture = await render('/en/items', sourceOf(WHOLE).source);
    all(fixture, '.bar__trigger')[0].click();
    await fixture.whenStable();
    const sheet = document.querySelector('lodb-filter-sheet');
    expect(sheet?.querySelectorAll('.chip')).toHaveLength(3);
  });
});
