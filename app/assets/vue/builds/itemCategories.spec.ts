import { describe, expect, it } from 'vitest'
import type { ItemOption } from './catalogTypes'
import { ITEM_CATEGORY_KEYS, matchesCategory } from './itemCategories'

function item(tags: string[]): ItemOption {
    return { id: '1', name: 'x', image: null, gold: 0, purchasable: true, tags }
}

describe('itemCategories', () => {
    it('leads with the all sentinel then the five buckets', () => {
        expect(ITEM_CATEGORY_KEYS[0]).toBe('all')
        expect(ITEM_CATEGORY_KEYS).toEqual(['all', 'attack', 'magic', 'defense', 'mobility', 'utility'])
    })

    it('the all bucket accepts every item, tags or not', () => {
        expect(matchesCategory(item([]), 'all')).toBe(true)
        expect(matchesCategory(item(['SpellDamage']), 'all')).toBe(true)
    })

    it('matches a bucket on any shared tag (OR semantics)', () => {
        expect(matchesCategory(item(['Damage']), 'attack')).toBe(true)
        expect(matchesCategory(item(['SpellDamage', 'Mana']), 'magic')).toBe(true)
        expect(matchesCategory(item(['Vision']), 'utility')).toBe(true)
        expect(matchesCategory(item(['Boots']), 'mobility')).toBe(true)
    })

    it('rejects an item whose tags miss the bucket', () => {
        expect(matchesCategory(item(['SpellDamage']), 'attack')).toBe(false)
        expect(matchesCategory(item([]), 'defense')).toBe(false)
    })

    it('lets a multi-tag item belong to several buckets', () => {
        const doransBlade = item(['Damage', 'LifeSteal', 'Health'])
        expect(matchesCategory(doransBlade, 'attack')).toBe(true)
        expect(matchesCategory(doransBlade, 'defense')).toBe(true)
        expect(matchesCategory(doransBlade, 'magic')).toBe(false)
    })
})
