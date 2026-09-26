/**
 * The filter chips of the armory, in their order: `all`, which filters nothing, then the
 * shopping buckets `matchesCategory` lays over Data Dragon's tags. Their labels are
 * `build.editor.armory.categories.<category>`.
 */
export const ITEM_CATEGORIES = [
  'all',
  'attack',
  'magic',
  'defense',
  'mobility',
  'utility',
] as const;

export type ItemCategory = (typeof ITEM_CATEGORIES)[number];
