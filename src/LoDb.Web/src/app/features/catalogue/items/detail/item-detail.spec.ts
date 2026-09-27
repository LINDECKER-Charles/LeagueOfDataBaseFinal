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
  upgrades: [],
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
    {
      id: '3114',
      name: 'Forbidden Idol',
      canonicalPath: 'items/3114-idol',
      image: IMAGE,
      edition: 'modern',
      gold: 800,
    },
  ],
  neighbours: { previous: null, next: null },
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

async function render(entry = ENTRY) {
  const heads: SeoPage[] = [];
  const write = (_: string, build: (texts: CatalogueTexts) => SeoPage) => heads.push(build(TEXTS));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'en/items/:id', component: ItemDetail, data: { entry } }]),
      provideTransloco({
        config: {
          availableLangs: ['en'],
          defaultLang: 'en',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = () => of({ item: { detail: { tier: 'Tier {{ depth }}' } } });
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
    const badge = element.querySelector('header lodb-edition-badge span');
    expect(badge?.textContent).toContain('edition.classic');
    // The hero wears the chip's own size, set apart from the version; cards keep the compact one.
    expect([...(badge?.classList ?? [])].sort()).toEqual(['hx-chip-hex', 'ms-3', 'shrink-0']);
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

  it('prices the item in gold, the coin of the legacy after the figure', async () => {
    const { element } = await render();

    const price = element.querySelector('[data-testid="price"]');
    expect(price?.textContent?.trim()).toBe('250');
    expect(price?.querySelector('lodb-icon')?.getAttribute('name')).toBe('gold');
  });

  it('prices each possible evolution under its name', async () => {
    const { element } = await render();

    const upgrade = element.querySelector('[data-testid="upgrades"] a');
    expect(upgrade?.textContent?.replace(/\s+/g, ' ').trim()).toBe('Forbidden Idol 800');
  });

  it('draws the icon of each stat the legacy had art for', async () => {
    const stats: ItemCard['stats'] = [
      { stat: 'attack_damage', isPercent: false, value: 75 },
      { stat: 'crit_chance', isPercent: true, value: 0.25 },
    ];
    const details = { ...DETAILS, profile: { ...CARD, stats } };
    const { element } = await render({ context: CONTEXT, details });

    const icons = [...element.querySelectorAll('lodb-item-aside .stat-row img')];
    expect(icons.map((icon) => icon.getAttribute('src'))).toEqual([
      '/icons/stats/attack_damage.png',
    ]);
  });

  it('states its tier from its depth and ARAM by its acronym, in a landmark', async () => {
    const details: ItemDetails = { ...DETAILS, depth: 3, availableMaps: [11, 12] };
    const { element } = await render({ context: CONTEXT, details });

    const aside = element.querySelector('[role="complementary"]');
    const chips = [...(aside?.querySelectorAll('.hx-chip') ?? [])].map((chip) =>
      chip.textContent?.trim(),
    );
    expect(chips.slice(0, 3)).toEqual(['Tier 3', 'map.11', 'items.maps.aram']);
  });

  it('marks an item without art by its initials, a related one by its id, as the legacy did', async () => {
    const absent: CatalogImage = { status: 'absent' };
    const recipe = DETAILS.recipe && {
      ...DETAILS.recipe,
      components: DETAILS.recipe.components.map((node) => ({ ...node, image: absent })),
    };
    const upgrades = DETAILS.upgrades.map((upgrade) => ({ ...upgrade, image: absent }));
    const profile = { ...CARD, image: absent };
    const details = { ...DETAILS, profile, recipe, upgrades };
    const { element } = await render({ context: CONTEXT, details });

    expect(element.querySelector('header [lodbFrame]')?.textContent?.trim()).toBe('FA');
    const component = element.querySelector('a.recipe-node[data-id="1027"] .recipe-node__icon');
    expect(component?.textContent?.trim()).toBe('1027');
    expect(element.querySelector('[data-testid="upgrades"] a span')?.textContent?.trim()).toBe(
      '3114',
    );
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
