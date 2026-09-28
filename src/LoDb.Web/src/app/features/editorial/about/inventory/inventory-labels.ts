import type { InventoryFact } from './inventory-fact';

/** The translation key labelling each fact, as on the current site. */
export const INVENTORY_LABELS: Record<InventoryFact, string> = {
  version: 'about.data.snapshot.patch',
  champions: 'header.navigation.champion',
  items: 'header.navigation.item',
  runes: 'header.navigation.runes',
  summoners: 'header.navigation.summoner',
  languages: 'about.data.snapshot.languages',
  versions: 'about.data.snapshot.patches',
};
