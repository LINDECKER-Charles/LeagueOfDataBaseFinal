import { ITEM_CATEGORIES } from './item-categories';
import { matchesCategory } from './matches-category';

function item(tags: string[]): { tags: string[] } {
  return { tags };
}

describe('item categories', () => {
  it('lead with the all bucket, then the five others', () => {
    expect(ITEM_CATEGORIES).toEqual(['all', 'attack', 'magic', 'defense', 'mobility', 'utility']);
  });

  it('let the all bucket take every item, tagged or not', () => {
    expect(matchesCategory(item([]), 'all')).toBe(true);
    expect(matchesCategory(item(['SpellDamage']), 'all')).toBe(true);
  });

  it('match a bucket on any tag it shares', () => {
    expect(matchesCategory(item(['Damage']), 'attack')).toBe(true);
    expect(matchesCategory(item(['SpellDamage', 'Mana']), 'magic')).toBe(true);
    expect(matchesCategory(item(['Vision']), 'utility')).toBe(true);
    expect(matchesCategory(item(['Boots']), 'mobility')).toBe(true);
  });

  it('refuse an item whose tags miss the bucket', () => {
    expect(matchesCategory(item(['SpellDamage']), 'attack')).toBe(false);
    expect(matchesCategory(item([]), 'defense')).toBe(false);
  });

  it('let an item with several tags sit in several buckets', () => {
    const doransBlade = item(['Damage', 'LifeSteal', 'Health']);

    expect(matchesCategory(doransBlade, 'attack')).toBe(true);
    expect(matchesCategory(doransBlade, 'defense')).toBe(true);
    expect(matchesCategory(doransBlade, 'magic')).toBe(false);
  });
});
