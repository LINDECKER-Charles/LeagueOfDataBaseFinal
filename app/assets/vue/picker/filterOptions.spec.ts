import { describe, expect, it } from 'vitest'
import { filterOptions, normalizeSearchText, type PickerEntry } from './filterOptions'

function entry(overrides: Partial<PickerEntry> & { id: string; name: string }): PickerEntry {
    return {
        image: null,
        searchText: normalizeSearchText(overrides.name),
        ...overrides,
    }
}

const AHRI = entry({ id: 'Ahri', name: 'Ahri' })
const SERAPHINE = entry({ id: 'Seraphine', name: 'Séraphine' })
const KAISA = entry({ id: 'Kaisa', name: "Kai'Sa" })
const FLAT = [AHRI, SERAPHINE, KAISA]

describe('normalizeSearchText', () => {
    it('lowercases and strips accents', () => {
        expect(normalizeSearchText('Séraphine')).toBe('seraphine')
        expect(normalizeSearchText('MAÎTRE YI')).toBe('maitre yi')
    })
})

describe('filterOptions', () => {
    it('returns every entry for an empty or blank query', () => {
        expect(filterOptions(FLAT, '')).toEqual(FLAT)
        expect(filterOptions(FLAT, '   ')).toEqual(FLAT)
    })

    it('matches case-insensitively', () => {
        expect(filterOptions(FLAT, 'AHRI')).toEqual([AHRI])
    })

    it('matches accent-insensitively, both ways', () => {
        expect(filterOptions(FLAT, 'seraphine')).toEqual([SERAPHINE])
        expect(filterOptions(FLAT, 'SÉRA')).toEqual([SERAPHINE])
    })

    it('preserves input order (stable) and never resorts', () => {
        const shuffled = [KAISA, AHRI, SERAPHINE]
        expect(filterOptions(shuffled, 'a')).toEqual([KAISA, AHRI, SERAPHINE])
    })

    it('returns an empty list when nothing matches', () => {
        expect(filterOptions(FLAT, 'zzzz')).toEqual([])
    })

    describe('rune groups', () => {
        const DOMINATION = entry({ id: '8100', name: 'Domination', isGroup: true })
        const ELECTROCUTE = entry({ id: '8112', name: 'Électrocution', groupId: '8100' })
        const PREDATOR = entry({ id: '8124', name: 'Prédateur', groupId: '8100' })
        const PRECISION = entry({ id: '8000', name: 'Précision', isGroup: true })
        const PTA = entry({ id: '8005', name: 'Jeu offensif', groupId: '8000' })
        const GROUPED = [DOMINATION, ELECTROCUTE, PREDATOR, PRECISION, PTA]

        it('keeps the header of a matching perk, drops unrelated groups', () => {
            expect(filterOptions(GROUPED, 'electro')).toEqual([DOMINATION, ELECTROCUTE])
        })

        it('a matching header keeps all of its perks', () => {
            expect(filterOptions(GROUPED, 'précision')).toEqual([PRECISION, PTA])
        })
    })
})
