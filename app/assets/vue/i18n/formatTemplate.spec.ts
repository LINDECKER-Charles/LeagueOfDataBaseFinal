import { describe, expect, it } from 'vitest'
import { formatTemplate } from './formatTemplate'

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
