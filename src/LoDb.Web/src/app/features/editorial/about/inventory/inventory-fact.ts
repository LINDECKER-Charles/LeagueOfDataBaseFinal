import type { InventorySnapshot } from './inventory-snapshot';

/** One fact of an {@link InventorySnapshot}, shown as a tile. */
export type InventoryFact = keyof InventorySnapshot;
