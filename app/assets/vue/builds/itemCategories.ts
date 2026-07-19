import type { ItemOption } from './catalogTypes'

/**
 * Player-facing shopping buckets layered over Data Dragon's raw item tags.
 * DDragon tags are fine-grained and English-only; the armory groups them into a
 * handful of readable filters. Matching is OR over a bucket's tags, so an item
 * can legitimately match several buckets (e.g. Doran's Blade → attack + defense).
 * `all` is the sentinel "no filter" bucket and owns no tags. Labels are resolved
 * from i18n by `key` — this module only owns the tag→bucket knowledge.
 */
export type ItemCategoryKey = 'all' | 'attack' | 'magic' | 'defense' | 'mobility' | 'utility'

export const ITEM_CATEGORY_ALL: ItemCategoryKey = 'all'

/** Display order of the filter chips (the sentinel `all` leads). */
export const ITEM_CATEGORY_KEYS: readonly ItemCategoryKey[] = [
    'all',
    'attack',
    'magic',
    'defense',
    'mobility',
    'utility',
]

/** DDragon tags mapped into each bucket (the `all` sentinel is intentionally absent). */
const CATEGORY_TAGS: Record<Exclude<ItemCategoryKey, 'all'>, readonly string[]> = {
    attack: ['Damage', 'AttackSpeed', 'CriticalStrike', 'ArmorPenetration', 'LifeSteal', 'OnHit'],
    magic: ['SpellDamage', 'MagicPenetration', 'Mana', 'ManaRegen', 'SpellVamp', 'CooldownReduction', 'AbilityHaste'],
    defense: ['Health', 'HealthRegen', 'Armor', 'SpellBlock', 'Tenacity'],
    mobility: ['Boots', 'NonbootsMovement'],
    utility: ['Consumable', 'Trinket', 'Vision', 'GoldPer', 'Jungle', 'Lane', 'Active', 'Aura', 'Slow', 'Stealth'],
}

/** Whether an item belongs to a bucket; the `all` sentinel accepts everything. */
export function matchesCategory(item: ItemOption, category: ItemCategoryKey): boolean {
    if (category === 'all') return true
    const tags = CATEGORY_TAGS[category]
    return item.tags.some((tag) => tags.includes(tag))
}
