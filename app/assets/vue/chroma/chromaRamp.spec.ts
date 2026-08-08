import { describe, expect, it } from 'vitest'
import { chromaRamp } from './chromaRamp'

describe('chromaRamp', () => {
    it('builds a two-stop diagonal from the accent pair', () => {
        expect(chromaRamp(['#ff0000', '#0000ff']))
            .toBe('linear-gradient(135deg, #ff0000, #0000ff)')
    })

    it('repeats a lone accent so the ramp stays flat instead of breaking', () => {
        expect(chromaRamp(['#ff0000'])).toBe('linear-gradient(135deg, #ff0000, #ff0000)')
    })

    it('falls back to the gold token when a chroma carries no accent', () => {
        expect(chromaRamp([]))
            .toBe('linear-gradient(135deg, var(--color-gold), var(--color-gold))')
    })
})
