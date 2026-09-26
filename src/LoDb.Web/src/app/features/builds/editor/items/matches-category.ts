import type { ItemOption } from '../../../../core/api/generated/models/item-option';
import type { ItemCategory } from './item-categories';

// The Data Dragon tags of each bucket. Data Dragon's tags are fine-grained and English only:
// the armory groups them into a few readable filters.
const CATEGORY_TAGS: Readonly<Record<Exclude<ItemCategory, 'all'>, readonly string[]>> = {
  attack: ['Damage', 'AttackSpeed', 'CriticalStrike', 'ArmorPenetration', 'LifeSteal', 'OnHit'],
  magic: [
    'SpellDamage',
    'MagicPenetration',
    'Mana',
    'ManaRegen',
    'SpellVamp',
    'CooldownReduction',
    'AbilityHaste',
  ],
  defense: ['Health', 'HealthRegen', 'Armor', 'SpellBlock', 'Tenacity'],
  mobility: ['Boots', 'NonbootsMovement'],
  utility: [
    'Consumable',
    'Trinket',
    'Vision',
    'GoldPer',
    'Jungle',
    'Lane',
    'Active',
    'Aura',
    'Slow',
    'Stealth',
  ],
};

/**
 * Whether an item belongs to a bucket: any one of its tags will do, so an item may sit in
 * several (Doran's Blade: attack and defense). `all` takes every item.
 */
export function matchesCategory(item: Pick<ItemOption, 'tags'>, category: ItemCategory): boolean {
  if (category === 'all') {
    return true;
  }
  const tags = CATEGORY_TAGS[category];
  return item.tags.some((tag) => tags.includes(tag));
}
