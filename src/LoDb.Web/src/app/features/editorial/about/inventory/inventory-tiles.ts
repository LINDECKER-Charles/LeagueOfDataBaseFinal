import type { FactTile } from './fact-tile';
import type { InventoryFact } from './inventory-fact';
import { INVENTORY_LABELS } from './inventory-labels';
import type { InventorySnapshot } from './inventory-snapshot';

/** What a tile shows while its number is loading or unknown. */
const UNKNOWN = '—';

/**
 * The tiles of the facts asked for, in that order: `—` for a fact the snapshot could not
 * read, and for all of them before it arrives (the prerendered placeholder).
 */
export function inventoryTiles(
  facts: readonly InventoryFact[],
  snapshot: InventorySnapshot | null,
): readonly FactTile[] {
  return facts.map((fact) => {
    const value = snapshot?.[fact] ?? null;
    return { label: INVENTORY_LABELS[fact], value: value === null ? UNKNOWN : String(value) };
  });
}
