import { describe, expect, it } from 'vitest'
import { chromaLabel } from './chromaLabel'

function labelOf(hex: string | undefined, name = 'Spirit Blossom Ahri'): string {
    return chromaLabel({ name, colors: hex === undefined ? [] : [hex] })
}

describe('chromaLabel', () => {
    it('prefers a parenthetical variant name provided by the patch', () => {
        expect(chromaLabel({ name: 'Spirit Blossom Ahri (Ruby)', colors: ['#0ac8b9'] }))
            .toBe('Ruby')
    })

    it('falls back to Chroma when no colour is readable', () => {
        expect(labelOf(undefined)).toBe('Chroma')
        expect(labelOf('not-a-hex')).toBe('Chroma')
        expect(labelOf('#abc')).toBe('Chroma')
    })

    it('names greys by lightness tier rather than by hue', () => {
        expect(labelOf('#ffffff')).toBe('Pearl')
        expect(labelOf('#000000')).toBe('Obsidian')
        expect(labelOf('#808080')).toBe('Steel')
    })

    it('collapses near-black and near-white accents to the achromatic tiers', () => {
        expect(labelOf('#0a0020')).toBe('Obsidian')
        expect(labelOf('#fffef0')).toBe('Pearl')
    })

    it('buckets saturated accents by hue', () => {
        expect(labelOf('#ff0000')).toBe('Crimson')
        expect(labelOf('#ffaa00')).toBe('Amber')
        expect(labelOf('#00ff00')).toBe('Emerald')
        expect(labelOf('#0ac8b9')).toBe('Teal')
        expect(labelOf('#0000ff')).toBe('Azure') // hue 240 is the Azure ceiling
        expect(labelOf('#7a2bff')).toBe('Sapphire')
        expect(labelOf('#ff00ff')).toBe('Violet')
        expect(labelOf('#ff0080')).toBe('Rose')
    })

    it('accepts a hex with or without its leading #', () => {
        expect(labelOf('c8aa6e')).toBe(labelOf('#c8aa6e'))
    })
})
