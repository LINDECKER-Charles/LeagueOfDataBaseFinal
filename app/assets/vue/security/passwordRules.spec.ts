import { describe, expect, it } from 'vitest'
import { evaluatePasswordRules, passwordsMatch, PASSWORD_MIN_LENGTH } from './passwordRules'

function ruleMap(password: string): Record<string, boolean> {
    return Object.fromEntries(evaluatePasswordRules(password).map((r) => [r.id, r.satisfied]))
}

describe('evaluatePasswordRules', () => {
    it('accepts a CNIL-compliant password on every criterion', () => {
        expect(ruleMap('Corr3ct-horse-Battery!')).toEqual({
            length: true,
            lowercase: true,
            uppercase: true,
            digit: true,
            special: true,
        })
    })

    it('fails everything on the empty string', () => {
        expect(ruleMap('')).toEqual({
            length: false,
            lowercase: false,
            uppercase: false,
            digit: false,
            special: false,
        })
    })

    it('flags each missing criterion independently', () => {
        expect(ruleMap('alllowercase1!x').uppercase).toBe(false)
        expect(ruleMap('ALLUPPERCASE1!X').lowercase).toBe(false)
        expect(ruleMap('NoDigitsHere!!').digit).toBe(false)
        expect(ruleMap('NoSpecial12345').special).toBe(false)
    })

    it('counts length in code points, not UTF-16 units (parity with PHP mb_strlen)', () => {
        // 11 emoji = 22 UTF-16 units but only 11 code points: still too short.
        expect(ruleMap('💎'.repeat(11)).length).toBe(false)
        expect(ruleMap('💎'.repeat(PASSWORD_MIN_LENGTH)).length).toBe(true)
    })

    it('treats any non-alphanumeric as special: punctuation, space, unicode symbol', () => {
        for (const candidate of ['with space', 'semi;colon', 'em—dash', 'caret^']) {
            expect(ruleMap(candidate).special, candidate).toBe(true)
        }
        expect(ruleMap('OnlyAlnum123').special).toBe(false)
    })

    it('recognises accented letters as lowercase/uppercase', () => {
        expect(ruleMap('été').lowercase).toBe(true)
        expect(ruleMap('ÉTÉ').uppercase).toBe(true)
    })
})

describe('passwordsMatch', () => {
    it('is true only for identical non-empty values', () => {
        expect(passwordsMatch('Abc-1234-defg', 'Abc-1234-defg')).toBe(true)
        expect(passwordsMatch('Abc-1234-defg', 'Abc-1234-defG')).toBe(false)
        expect(passwordsMatch('', '')).toBe(false)
    })
})
