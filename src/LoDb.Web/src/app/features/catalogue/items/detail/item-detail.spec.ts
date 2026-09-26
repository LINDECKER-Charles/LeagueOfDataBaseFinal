import { TestBed } from '@angular/core/testing';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { CatalogImage } from '../../../../core/api/generated/models/catalog-image';
import type { ItemCard } from '../../../../core/api/generated/models/item-card';
import type { ItemDetails } from '../../../../core/api/generated/models/item-details';
import type { PageContext } from '../../../../core/context/page-context';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import type { SeoPage } from '../../../../core/seo/seo-page';
import { CatalogueLists } from '../../shared/data/catalogue-lists';
import { CatalogueHead } from '../../shared/codex/head/catalogue-head';
import type { CatalogueTexts } from '../../shared/codex/head/catalogue-texts';
import { ItemDetail } from './item-detail';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const IMAGE: CatalogImage = { status: 'present', url: '/cdn/blobs/a.png' };
const CARD: ItemCard = {
  canonicalPath: 'items/771004-faerie-charm',
  consumable: false,
  counterpart: {
    id: '1004',
    name: 'Faerie Charm',
    edition: 'modern',
    canonicalPath: 'items/1004-faerie-charm',
  },
  edition: 'classic',
  gold: { base: 250, isPurchasable: true, sell: 175, total: 250 },
  id: '771004',
  image: IMAGE,
  maps: [11],
  name: 'Faerie Charm',
  stats: [],
  summary: 'Slightly increases <stats>Mana Regen</stats>',
  tags: ['ManaRegen'],
};
const DETAILS: ItemDetails = {
  availableMaps: [453],
  canonicalPath: CARD.canonicalPath,
  description: '<mainText><stats>50% Base Mana Regen</stats></mainText>',
  language: 'en_US',
  version: CONTEXT.version,
  profile: CARD,
  recipe: {
    id: '771004',
    name: 'Faerie Charm',
    canonicalPath: CARD.canonicalPath,
    image: IMAGE,
    gold: 250,
    combine: 0,
    components: [
      {
        id: '1027',
        name: 'Sapphire',
        canonicalPath: 'items/1027-sapphire',
        image: IMAGE,
        gold: 250,
        combine: 0,
        components: [],
      },
    ],
  },
  upgrades: [
    { id: '3114', name: 'Forbidden Idol', canonicalPath: 'items/3114-idol', image: IMAGE },
  ],
};
const ENTRY: CatalogueEntry<ItemDetails> = { context: CONTEXT, details: DETAILS };
const TEXTS: CatalogueTexts = {
  seo: (key, params) => `${key} ${JSON.stringify(params ?? {})}`,
  main: (key) => (key === 'edition.classic' ? 'LoL Classic' : key),
};
const NEIGHBOURS = ['1001-boots', '771004-faerie-charm', '1027-sapphire'].map((path) => ({
  ...CARD,
  id: path.split('-')[0] ?? path,
  name: path,
  canonicalPath: `items/${path}`,
}));

async function render() {
  const heads: SeoPage[] = [];
  const write = (_: string, build: (texts: CatalogueTexts) => SeoPage) => heads.push(build(TEXTS));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'en/items/:id', component: ItemDetail, data: { entry: ENTRY } }]),
      provideTransloco({
        config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
        loader: class {
          getTranslation = () => of({});
        },
      }),
      { provide: CatalogueHead, useValue: { write } },
      {
        provide: CatalogueLists,
        useValue: { fetch: () => of({ kind: 'list', list: { entries: NEIGHBOURS } }) },
      },
    ],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/en/items/771004-faerie-charm');
  await harness.fixture.whenStable();
  return { element: harness.routeNativeElement as HTMLElement, heads };
}

describe('lodb-item-detail', () => {
  beforeEach(() => {
    // Reduced motion: lodbReveal shows the sections at once, without an observer.
    vi.stubGlobal('matchMedia', () => ({ matches: true }));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the item, its edition and the link to its current twin', async () => {
    const { element } = await render();

    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Faerie Charm');
    expect(element.querySelector('lodb-edition-badge')?.textContent).toContain('edition.classic');
    const twin = element.querySelector<HTMLAnchorElement>('lodb-edition-counterpart a');
    expect(twin?.getAttribute('href')).toBe('/en/items/1004-faerie-charm');
    expect(twin?.dataset['edition']).toBe('modern');
  });

  it('draws the recipe tree and the upgrades as links, the description as rich text', async () => {
    const { element } = await render();

    const component = element.querySelector('lodb-recipe-tree a.recipe-node');
    expect(component?.getAttribute('href')).toBe('/en/items/1027-sapphire');
    const upgrade = element.querySelector('[data-testid="upgrades"] a');
    expect(upgrade?.getAttribute('href')).toBe('/en/items/3114-idol');
    expect(element.querySelector('maintext stats')?.textContent).toBe('50% Base Mana Regen');
  });

  it('names the LoL Classic edition in its head, and turns the pages of the list', async () => {
    const { element, heads } = await render();

    expect(heads.at(-1)?.title).toContain('"name":"Faerie Charm (LoL Classic)"');
    expect(element.querySelector('lodb-pager a[rel="prev"]')?.getAttribute('href')).toBe(
      '/en/items/1001-boots',
    );
    expect(element.querySelector('lodb-pager a[rel="next"]')?.getAttribute('href')).toBe(
      '/en/items/1027-sapphire',
    );
  });
});
