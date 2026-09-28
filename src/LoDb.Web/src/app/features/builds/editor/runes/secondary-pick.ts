/** A rune of the secondary tree, with the minor row it was picked in. */
export interface SecondaryPick {
  /** The row in the tree, from 1; `RUNE_LIMITS.ghostSlot` for a rune no loaded tree holds. */
  readonly slotIndex: number;
  readonly perkId: number;
}
