import type { SummonerDetails } from '../../../../../core/api/generated/models/summoner-details';
import type { Translate } from '../../../shared/codex/texts/translate';
import { summonerPlaquesOf } from './summoner-plaques-of';

const t: Translate = (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key);

const FLASH: SummonerDetails = {
  canonicalPath: 'summoners/SummonerFlash',
  language: 'en_US',
  version: '16.19.1',
  cost: [0],
  costType: 'No Cost',
  globalRange: false,
  range: [425],
  neighbours: { previous: null, next: null },
  profile: {
    canonicalPath: 'summoners/SummonerFlash',
    cooldown: [300],
    description: 'Teleports',
    edition: 'modern',
    id: 'SummonerFlash',
    image: { status: 'present', url: '/cdn/blobs/flash.png' },
    key: '4',
    modes: [],
    name: 'Flash',
    summonerLevel: 7,
  },
};

describe('summonerPlaquesOf', () => {
  it('engraves the range and the unlock level, not a cost the spell does not have', () => {
    expect(summonerPlaquesOf(FLASH, t)).toEqual([
      { label: 'summoner.detail.fields.range', value: '425' },
      {
        label: 'summoner.detail.fields.summoner_level',
        value: 'summoner.detail.unlocked_at {"level":7}',
      },
    ]);
  });

  it('says "Global" for the whole map, and engraves a real cost and the charges', () => {
    const spell = {
      ...FLASH,
      globalRange: true,
      range: [25000],
      cost: [50],
      costType: 'Mana',
      charges: 2,
    };

    expect(summonerPlaquesOf(spell, t)).toEqual([
      { label: 'summoner.detail.fields.range', value: 'summoner.detail.range_global' },
      expect.objectContaining({ label: 'summoner.detail.fields.summoner_level' }),
      { label: 'summoner.detail.fields.cost', value: '50 Mana' },
      { label: 'summoner.detail.fields.maxammo', value: '2' },
    ]);
  });

  it('engraves nothing the data leaves out', () => {
    const spell = {
      ...FLASH,
      range: [],
      cost: [50],
      costType: null,
      profile: { ...FLASH.profile, summonerLevel: null },
    };

    expect(summonerPlaquesOf(spell, t)).toEqual([]);
  });
});
