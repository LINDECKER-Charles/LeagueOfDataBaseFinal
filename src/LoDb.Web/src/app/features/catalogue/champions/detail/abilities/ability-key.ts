import type { AbilitySlot } from '../../../../../core/api/generated/models/ability-slot';

/** The key a player casts an ability with, P standing for the passive. */
export function abilityKey(slot: AbilitySlot): string {
  return slot === 'passive' ? 'P' : slot.toUpperCase();
}
