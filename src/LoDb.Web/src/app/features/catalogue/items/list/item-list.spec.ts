import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { ItemCard } from '../../../../core/api/generated/models/item-card';
import type { PageContext } from '../../../../core/context/page-context';
import type { SeoPage } from '../../../../core/seo/seo-page';
import { CatalogueLists } from '../../shared/data/catalogue-lists';
import { CatalogueHead } from '../../shared/codex/head/catalogue-head';
import type { CatalogueTexts } from '../../shared/codex/head/catalogue-texts';
import { ItemList } from './item-list';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const CARDS: ItemCard[] = ['1001-boots', '771004-faerie-charm'].map((path, index) => ({
  canonicalPath: `items/${path}`,
  consumable: false,
  edition: index === 0 ? 'modern' : 'classic',
  gold: { base: 300, isPurchasable: true, sell: 210, total: 300 },
  id: path.split('-')[0] ?? path,
  image: { status: 'present', url: `/cdn/blobs/${path}.png` },
  maps: [11],
  name: path,
  stats: [{ stat: 'move_speed', value: 25, isPercent: false }],
  summary: '<stats>+25 Move Speed</stats>',
  tags: ['Boots'],
}));
const TEXTS: CatalogueTexts = {
  seo: (key, params) => `${key} ${JSON.stringify(params ?? {})}`,
  main: (key) => key,
};

async function render() {
  const heads: SeoPage[] = [];
  const write = (_: string, build: (texts: CatalogueTexts) => SeoPage) => heads.push(build(TEXTS));
  const list = { entries: CARDS, total: CARDS.length, facets: { tags: ['Boots'] } };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'en/items', component: ItemList, data: { context: CONTEXT } }]),
      provideTransloco({
        config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
        loader: class {
          getTranslation = () => of({});
        },
      }),
      { provide: CatalogueHead, useValue: { write } },
      {
        provide: CatalogueLists,
        useValue: { fetch: () => of({ kind: 'list', list, retryAfterMs: null }) },
      },
    ],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/en/items');
  await harness.fixture.whenStable();
  return { element: harness.routeNativeElement as HTMLElement, heads };
}

describe('lodb-item-list', () => {
  it('lists every item with its price, each card linking its page', async () => {
    const { element } = await render();

    const links = [...element.querySelectorAll('lodb-entity-card a')];
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/en/items/1001-boots',
      '/en/items/771004-faerie-charm',
    ]);
    expect(element.querySelectorAll('lodb-entity-card .stat-cell')).toHaveLength(4);
    expect(element.querySelector('h1')?.textContent).toContain('item.list.header');
  });

  it('marks the LoL Classic twin of an item', async () => {
    const { element } = await render();

    const badges = [...element.querySelectorAll('lodb-entity-card')].map((card) =>
      card.textContent?.includes('edition.classic'),
    );
    expect(badges).toEqual([false, true]);
  });

  it('writes the head of the list with its count', async () => {
    const { heads } = await render();

    expect(heads.at(-1)?.description).toBe('item.list.description {"count":2,"version":"16.19.1"}');
    expect(heads.at(-1)?.image).toBe('/preview/items.png');
  });
});
