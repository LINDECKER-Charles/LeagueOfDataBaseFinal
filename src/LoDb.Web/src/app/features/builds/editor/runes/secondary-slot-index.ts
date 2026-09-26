import type { RuneTreeOption } from '../../../../core/api/generated/models/rune-tree-option';
import { RUNE_LIMITS } from './rune-limits';

/**
 * The minor row of a tree holding a rune, from 1; null when the tree or the rune is not in
 * the loaded trees, or when the rune is a keystone, which a secondary tree never offers.
 */
export function secondarySlotIndex(
  trees: readonly RuneTreeOption[],
  styleId: number,
  perkId: number,
): number | null {
  const slots = trees.find((tree) => tree.id === styleId)?.slots ?? [];
  const index = slots.findIndex((runes) => runes.some((rune) => rune.id === perkId));
  return index > RUNE_LIMITS.keystoneSlot ? index : null;
}
