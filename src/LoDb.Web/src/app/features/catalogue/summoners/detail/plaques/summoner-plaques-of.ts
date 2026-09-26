import type { SummonerDetails } from '../../../../../core/api/generated/models/summoner-details';
import type { Translate } from '../../../shared/codex/texts/translate';
import type { Plaque } from './plaque';

// Data Dragon's ways of saying a spell costs nothing.
const NO_COST_TYPES: ReadonlySet<string> = new Set(['', 'no cost', 'nocost']);

function rangeOf(spell: SummonerDetails, t: Translate): Plaque[] {
  const [range] = spell.range;
  if (range === undefined) {
    return [];
  }
  const value = spell.globalRange ? t('summoner.detail.range_global') : String(range);
  return [{ label: t('summoner.detail.fields.range'), value }];
}

function levelOf(spell: SummonerDetails, t: Translate): Plaque[] {
  const level = spell.profile.summonerLevel;
  return level
    ? [
        {
          label: t('summoner.detail.fields.summoner_level'),
          value: t('summoner.detail.unlocked_at', { level }),
        },
      ]
    : [];
}

function costOf(spell: SummonerDetails, t: Translate): Plaque[] {
  const [cost] = spell.cost;
  const type = spell.costType?.trim() ?? '';
  if (!cost || NO_COST_TYPES.has(type.toLowerCase())) {
    return [];
  }
  return [{ label: t('summoner.detail.fields.cost'), value: `${cost} ${type}` }];
}

function chargesOf(spell: SummonerDetails, t: Translate): Plaque[] {
  const charges = spell.charges ?? 0;
  return charges > 0
    ? [{ label: t('summoner.detail.fields.maxammo'), value: String(charges) }]
    : [];
}

/**
 * The plaques of a summoner spell page, significant facts only, as the legacy page kept
 * them: its range ("Global" for the whole map), its unlock level, a cost it really has, and
 * its charges when it stacks them.
 */
export function summonerPlaquesOf(spell: SummonerDetails, t: Translate): Plaque[] {
  return [...rangeOf(spell, t), ...levelOf(spell, t), ...costOf(spell, t), ...chargesOf(spell, t)];
}
