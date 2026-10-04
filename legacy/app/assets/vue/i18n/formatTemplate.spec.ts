import { describe, expect, it } from 'vitest'
import { formatTemplate, splitTemplate } from './formatTemplate'

describe('formatTemplate', () => {
    it('substitutes every placeholder of the template', () => {
        expect(formatTemplate('%count% / %max%', { count: 3, max: 8 })).toBe('3 / 8')
    })

    it('substitutes EVERY occurrence, not just the first', () => {
        expect(formatTemplate('Level %level% (%level%)', { level: 7 }))
            .toBe('Level 7 (7)')
    })

    it('leaves unknown placeholders untouched', () => {
        expect(formatTemplate('%count% of %total%', { count: 2 })).toBe('2 of %total%')
    })

    it('accepts strings as well as numbers', () => {
        expect(formatTemplate('Hello %name%', { name: 'Ahri' })).toBe('Hello Ahri')
    })
})

describe('splitTemplate', () => {
    it('returns the text on each side of the placeholder', () => {
        expect(splitTemplate('%count% results', 'count')).toEqual({ before: '', after: ' results' })
        expect(splitTemplate('Showing %count% of all', 'count'))
            .toEqual({ before: 'Showing ', after: ' of all' })
    })

    it('keeps the whole text before an absent placeholder', () => {
        expect(splitTemplate('Results', 'count')).toEqual({ before: 'Results', after: '' })
    })
})
