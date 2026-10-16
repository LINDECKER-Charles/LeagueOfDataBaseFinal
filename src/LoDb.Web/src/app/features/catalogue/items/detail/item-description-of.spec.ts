import type { ItemCard } from '../../../../core/api/generated/models/item-card';
import type { ItemDetails } from '../../../../core/api/generated/models/item-details';
import type { PageContext } from '../../../../core/context/page-context';
import { ROOT_CATALOGUES } from '../../../../core/testing/root-catalogues';
import { SEO_CATALOGUES } from '../../../../core/testing/seo-catalogues';
import type { CatalogueTexts } from '../../shared/codex/head/catalogue-texts';
import { itemDescriptionOf } from './item-description-of';

const MAX_LENGTH = 160;
const CONTEXT: PageContext = { locale: 'en', version: '16.20.1', pinned: false, language: 'en_US' };
const CARD: ItemCard = {
  canonicalPath: 'items/3078-trinity-force',
  consumable: false,
  edition: 'modern',
  gold: { base: 133, isPurchasable: true, sell: 2333, total: 3333 },
  id: '3078',
  image: { status: 'present', url: '/cdn/blobs/a.png' },
  maps: [11, 12],
  name: 'Trinity Force',
  stats: [
    { stat: 'attack_damage', isPercent: false, value: 36 },
    { stat: 'attack_speed', isPercent: true, value: 0.3 },
    { stat: 'health', isPercent: false, value: 333 },
  ],
  summary: '',
  tags: [],
  upgrades: [],
};
const DETAILS: ItemDetails = {
  availableMaps: [],
  canonicalPath: CARD.canonicalPath,
  description: '',
  language: 'en_US',
  version: CONTEXT.version,
  profile: CARD,
  upgrades: [],
  neighbours: { previous: null, next: null },
};
const NOT_FOR_SALE: ItemCard['gold'] = { base: 0, isPurchasable: false, sell: 0, total: 0 };

function lookup(catalogue: object, key: string): string {
  const text = key
    .split('.')
    .reduce<unknown>((node, part) => (node as Record<string, unknown>)[part], catalogue);
  return String(text);
}

// The real catalogues of the locale, filled the way Transloco fills a `{{ param }}`.
function textsOf(locale: 'en' | 'ja'): CatalogueTexts {
  return {
    seo: (key, params = {}) =>
      lookup(SEO_CATALOGUES[locale], key).replace(/\{\{ (\w+) \}\}/g, (_, name: string) =>
        String(params[name]),
      ),
    main: (key) => lookup(ROOT_CATALOGUES[locale], key),
  };
}

function descriptionOf(
  card: Partial<ItemCard>,
  name = card.name ?? CARD.name,
  locale: 'en' | 'ja' = 'en',
) {
  const entry = {
    context: { ...CONTEXT, locale },
    details: { ...DETAILS, profile: { ...CARD, ...card } },
  };
  return itemDescriptionOf(entry, textsOf(locale), name);
}

describe('itemDescriptionOf', () => {
  it('states the price and the first three stats as the page shows them', () => {
    expect(descriptionOf({})).toBe(
      'Trinity Force in League of Legends: +36 Attack damage, +30 % Attack speed, and +333 ' +
        'Health for 3333 gold. Recipe and upgrades, up to date for patch 16.20.1.',
    );
  });

  it('names fewer stats when all of them would run past what a result page shows', () => {
    const description = descriptionOf(
      { name: 'Mercurial Scimitar' },
      'Mercurial Scimitar (LoL Classic)',
    );

    expect(description.length).toBeLessThanOrEqual(MAX_LENGTH);
    expect(description).toBe(
      'Mercurial Scimitar (LoL Classic) in League of Legends: +36 Attack damage for 3333 gold. ' +
        'Recipe and upgrades, up to date for patch 16.20.1.',
    );
  });

  it('states no price for an item that cannot be bought', () => {
    expect(descriptionOf({ gold: NOT_FOR_SALE, stats: CARD.stats.slice(1, 2) })).toBe(
      'Trinity Force in League of Legends: +30 % Attack speed — effects, recipe and upgrades, ' +
        'up to date for patch 16.20.1.',
    );
  });

  it('states the price alone for an item without a stat block, such as a consumable', () => {
    expect(descriptionOf({ name: 'Elixir of Sorcery', consumable: true, stats: [] })).toBe(
      'Elixir of Sorcery in League of Legends costs 3333 gold: what it does, its recipe and ' +
        'upgrades, up to date for patch 16.20.1.',
    );
  });

  it('keeps the plain description for an item with neither a price nor stats', () => {
    expect(descriptionOf({ name: 'Poro-Snax', gold: NOT_FOR_SALE, stats: [] })).toBe(
      'Poro-Snax in League of Legends: stats, cost, recipe and upgrades, up to date for patch ' +
        '16.20.1.',
    );
  });

  it('reads each stat and the list in the order of the locale', () => {
    expect(descriptionOf({}, CARD.name, 'ja')).toBe(
      'リーグ・オブ・レジェンドのTrinity Force：攻撃力+36、攻撃速度+30 %、体力+333、' +
        '価格3333ゴールド。レシピと派生アイテム。パッチ16.20.1対応。',
    );
  });
});
