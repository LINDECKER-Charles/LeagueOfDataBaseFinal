import { describe, expect, it } from 'vitest'
import { normalizeSearchText } from './normalizeSearchText'

describe('normalizeSearchText', () => {
    it('lowercases and strips accents', () => {
        expect(normalizeSearchText('Séraphine')).toBe('seraphine')
        expect(normalizeSearchText('MAÎTRE YI')).toBe('maitre yi')
    })

    it('leaves punctuation alone (apostrophes are part of champion names)', () => {
        expect(normalizeSearchText("Kai'Sa")).toBe("kai'sa")
    })
})
