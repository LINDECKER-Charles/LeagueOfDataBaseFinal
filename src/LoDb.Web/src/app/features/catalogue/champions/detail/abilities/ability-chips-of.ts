import type { ChampionAbility } from '../../../../../core/api/generated/models/champion-ability';
import type { AbilityChip } from './ability-chip';

interface Candidate {
  readonly label: string;
  readonly value: string | null;
  readonly unit: string;
}

function textOf(value: number | null | undefined): string | null {
  return value === null || value === undefined ? null : String(value);
}

function isShown(chip: Candidate): chip is AbilityChip {
  return chip.value !== null && chip.value !== '';
}

/**
 * The cast figures an ability has, in a fixed order. A free ability shows no cost, and the
 * passive, which has no rank, no rank count.
 */
export function abilityChipsOf(ability: ChampionAbility, resource: string): AbilityChip[] {
  const isFree = ability.cost === null || ability.cost === undefined || ability.cost === '0';
  const ranks = ability.slot === 'passive' ? null : textOf(ability.maxRank);
  const candidates: Candidate[] = [
    { label: 'champion.detail.cooldown', value: ability.cooldown ?? null, unit: 's' },
    {
      label: 'champion.detail.cost',
      value: isFree ? null : (ability.cost ?? null),
      unit: resource,
    },
    { label: 'champion.detail.range', value: ability.range ?? null, unit: '' },
    { label: 'champion.detail.charges', value: textOf(ability.charges), unit: '' },
    { label: 'champion.detail.ranks', value: ranks, unit: '' },
  ];
  return candidates.filter(isShown);
}
