/**
 * The shape of a rune page, the game client's: four primary slots, the keystone first, and
 * two secondary picks from two different minor rows.
 */
export const RUNE_LIMITS = {
  primarySlots: 4,
  secondaryPicks: 2,
  keystoneSlot: 0,
  /** The row of a stored secondary pick the loaded trees do not hold (yet, or at all). */
  ghostSlot: -1,
} as const;
