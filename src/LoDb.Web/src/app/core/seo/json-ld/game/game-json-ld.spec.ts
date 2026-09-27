import type { ChampionDetails } from '../../../api/generated/models/champion-details';
import type { ItemDetails } from '../../../api/generated/models/item-details';
import type { RuneTreeDetails } from '../../../api/generated/models/rune-tree-details';
import type { SummonerDetails } from '../../../api/generated/models/summoner-details';
import { seoUrlsOf } from '../../urls/seo-urls-of';
import { championJsonLd } from './champion-json-ld';
import { itemJsonLd } from './item-json-ld';
import { runePathJsonLd } from './rune-path-json-ld';
import { summonerSpellJsonLd } from './summoner-spell-json-ld';

const ORIGIN = 'https://league-of-data-base.com';
const IMAGE = { status: 'present', url: '/cdn/blobs/ab12.png' } as const;
const COMMON = {
  language: 'en_US',
  version: '16.19.1',
  neighbours: { previous: null, next: null },
};

function urlsOf(path: string) {
  return seoUrlsOf({ origin: ORIGIN, locale: 'en', version: null, path });
}

const AHRI: ChampionDetails = {
  ...COMMON,
  canonicalPath: 'champions/Ahri',
  abilities: [],
  allyTips: [],
  enemyTips: [],
  skins: [],
  art: { centered: '', loading: '', splash: 'https://ddragon.example/Ahri_0.jpg' },
  blurb: 'Innately connected to the magic of the spirit realm, <i>Ahri</i> is a fox-like vastaya.',
  profile: {
    canonicalPath: 'champions/Ahri',
    id: 'Ahri',
    key: '103',
    name: 'Ahri',
    title: 'the Nine-Tailed Fox',
    image: IMAGE,
    loadingArt: 'https://ddragon.leagueoflegends.com/cdn/img/champion/loading/Ahri_0.jpg',
    blurb: 'Innately connected to the magic of the spirit realm, Ahri is a fox-like vastaya.',
    partype: 'Mana',
    resource: 'mana',
    tags: ['Mage', 'Assassin'],
    ratings: { attack: 3, defense: 4, magic: 8, difficulty: 5 },
    stats: [
      { stat: 'health', base: 590, perLevel: 104 },
      { stat: 'mana', base: 418, perLevel: 25 },
      { stat: 'attack_range', base: 550 },
      { stat: 'attack_speed', base: 0.668, perLevel: 2.2 },
    ],
  },
};

const FAERIE_CHARM: ItemDetails = {
  ...COMMON,
  canonicalPath: 'items/1004-faerie-charm',
  availableMaps: [11, 12],
  description: '<mainText><stats><attention>50%</attention> Base Mana Regen</stats></mainText>',
  upgrades: [],
  profile: {
    canonicalPath: 'items/1004-faerie-charm',
    id: '1004',
    name: 'Faerie Charm',
    image: IMAGE,
    consumable: false,
    edition: 'modern',
    maps: [11, 12],
    stats: [],
    summary: 'Slightly increases Mana Regen.',
    tags: ['ManaRegen'],
    gold: { base: 200, total: 200, sell: 140, isPurchasable: true },
    upgrades: [],
  },
};

const PRECISION: RuneTreeDetails = {
  ...COMMON,
  canonicalPath: 'runes/8000-precision',
  profile: {
    canonicalPath: 'runes/8000-precision',
    id: 8000,
    key: 'Precision',
    name: 'Precision',
    image: { status: 'absent' },
  },
  slots: [
    { slot: 'keystone', runes: [] },
    { slot: 'row1', runes: [] },
    { slot: 'row2', runes: [] },
    { slot: 'row3', runes: [] },
  ],
};

const FLASH: SummonerDetails = {
  ...COMMON,
  canonicalPath: 'summoners/SummonerFlash',
  cost: [0],
  globalRange: false,
  range: [425],
  profile: {
    canonicalPath: 'summoners/SummonerFlash',
    id: 'SummonerFlash',
    key: '4',
    name: 'Flash',
    image: IMAGE,
    edition: 'modern',
    modes: [],
    cooldown: [300],
    summonerLevel: 7,
    description: 'Teleports your champion a short distance toward your cursor.',
  },
};

describe('game JSON-LD builders', () => {
  it('states a champion as the character of the game, with roles, ratings and stats', () => {
    expect(championJsonLd(AHRI, urlsOf(AHRI.canonicalPath))).toMatchSnapshot();
  });

  it('states an item as a game item priced in gold, never as a product', () => {
    const node = itemJsonLd(FAERIE_CHARM, urlsOf(FAERIE_CHARM.canonicalPath));

    expect(JSON.stringify(node)).not.toMatch(/Product|Offer|priceCurrency/);
    expect(node).toMatchSnapshot();
  });

  it('says an item that cannot be bought is not purchasable', () => {
    const relic = {
      ...FAERIE_CHARM,
      profile: {
        ...FAERIE_CHARM.profile,
        gold: { base: 0, total: 0, sell: 0, isPurchasable: false },
      },
    };

    expect(JSON.stringify(itemJsonLd(relic, urlsOf('items/1-relic')))).toContain(
      '{"@type":"PropertyValue","name":"Purchasable","value":"false"}',
    );
  });

  it('states a rune path with its slots, without the image it lacks', () => {
    expect(runePathJsonLd(PRECISION, urlsOf(PRECISION.canonicalPath))).toMatchSnapshot();
  });

  it('states a summoner spell with its rank-1 cooldown, range and unlock level', () => {
    expect(summonerSpellJsonLd(FLASH, urlsOf(FLASH.canonicalPath))).toMatchSnapshot();
  });

  it('leaves out a whole-map range, which is a sentinel and not a distance', () => {
    const teleport = { ...FLASH, globalRange: true, range: [25000] };

    expect(JSON.stringify(summonerSpellJsonLd(teleport, urlsOf('summoners/x')))).not.toContain(
      'Range',
    );
  });
});
