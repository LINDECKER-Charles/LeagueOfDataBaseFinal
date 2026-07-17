import { describe, expect, it } from 'vitest'
import { parseStructure, serializeStructure, type BuildStructure } from './structure'

const full: BuildStructure = {
    championId: 'Aatrox',
    runes: {
        primaryStyleId: 8000,
        primarySelections: [8005, 9101, 9104, 8014],
        secondaryStyleId: 8100,
        secondarySelections: [8126, 8138],
    },
    steps: [
        { label: 'Start', note: null, items: ['1055', '2003'] },
        { label: 'Core', note: 'rush boots', items: ['3006'] },
    ],
}

describe('parseStructure', () => {
    it('accepts an already-decoded object', () => {
        expect(parseStructure(full)).toEqual(full)
    })

    it('accepts a JSON string', () => {
        expect(parseStructure(JSON.stringify(full))).toEqual(full)
    })

    it('returns null on unusable input', () => {
        expect(parseStructure(null)).toBeNull()
        expect(parseStructure('{oops')).toBeNull()
        expect(parseStructure(42)).toBeNull()
        expect(parseStructure([1, 2])).toBeNull()
    })

    it('coerces numeric strings and numeric item ids', () => {
        const parsed = parseStructure({
            championId: 'Ahri',
            runes: {
                primaryStyleId: '8000',
                primarySelections: ['8005', 9101],
                secondaryStyleId: '8100',
                secondarySelections: ['8126'],
            },
            steps: [{ label: 'Start', note: '', items: [1055, '2003'] }],
        })
        expect(parsed).toEqual({
            championId: 'Ahri',
            runes: {
                primaryStyleId: 8000,
                primarySelections: [8005, 9101],
                secondaryStyleId: 8100,
                secondarySelections: [8126],
            },
            steps: [{ label: 'Start', note: null, items: ['1055', '2003'] }],
        })
    })

    it('defaults missing parts instead of failing', () => {
        expect(parseStructure({})).toEqual({
            championId: '',
            runes: { primaryStyleId: 0, primarySelections: [], secondaryStyleId: 0, secondarySelections: [] },
            steps: [],
        })
    })
})

describe('serializeStructure', () => {
    it('round-trips through parseStructure', () => {
        expect(parseStructure(serializeStructure(full))).toEqual(full)
    })

    it('emits exactly the persisted shape', () => {
        const decoded = JSON.parse(serializeStructure(full)) as Record<string, unknown>
        expect(Object.keys(decoded).sort()).toEqual(['championId', 'runes', 'steps'])
        expect(Object.keys(decoded.runes as object).sort()).toEqual([
            'primarySelections',
            'primaryStyleId',
            'secondarySelections',
            'secondaryStyleId',
        ])
    })
})
