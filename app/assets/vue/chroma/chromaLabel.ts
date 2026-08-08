/**
 * Chroma naming rule, pure and Vue-free. CommunityDragon rarely carries a
 * variant name (a chroma's `name` is usually just the base skin), so the label
 * is DERIVED from the accent hue — honest, because it describes the actual
 * colour instead of claiming a Riot product name. A parenthetical suffix, when
 * a patch does provide one ("… (Ruby)"), always wins.
 */

const HEX_TRIPLET = /^([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i
const PARENTHETICAL_SUFFIX = /\(([^)]+)\)\s*$/
const UNKNOWN_LABEL = 'Chroma'

/** Below this absolute chroma the swatch reads as a grey, whatever its hue. */
const ACHROMATIC_MAX_CHROMA = 0.1
const GREY_PEARL_MIN_LIGHTNESS = 0.75
const GREY_OBSIDIAN_MAX_LIGHTNESS = 0.2
/** Coloured swatches this dark / this bright lose their hue to the eye. */
const OBSIDIAN_MAX_LIGHTNESS = 0.15
const PEARL_MIN_LIGHTNESS = 0.92

/** Upper hue bound (degrees, inclusive) of each named bucket, in order. */
const HUE_BUCKETS: readonly [number, string][] = [
    [15, 'Crimson'], [40, 'Amber'], [65, 'Gold'], [150, 'Emerald'],
    [195, 'Teal'], [240, 'Azure'], [280, 'Sapphire'], [320, 'Violet'],
    [345, 'Rose'], [360, 'Crimson'],
]

interface Accent {
    /** Absolute chroma (max − min): reliable near black/white, unlike HSL saturation. */
    chroma: number
    lightness: number
    hue: number
}

interface Channels {
    red: number
    green: number
    blue: number
    max: number
    chroma: number
}

export function chromaLabel(chroma: { name: string; colors: string[] }): string {
    const suffix = chroma.name.match(PARENTHETICAL_SUFFIX)
    return suffix ? suffix[1] : colorName(chroma.colors[0])
}

/** Nearest descriptive colour name for a hex accent; `Chroma` when unreadable. */
function colorName(hex?: string): string {
    const accent = readAccent(hex)
    if (accent === null) return UNKNOWN_LABEL
    if (accent.chroma < ACHROMATIC_MAX_CHROMA) return greyName(accent.lightness)
    if (accent.lightness < OBSIDIAN_MAX_LIGHTNESS) return 'Obsidian'
    if (accent.lightness > PEARL_MIN_LIGHTNESS) return 'Pearl'
    return HUE_BUCKETS.find(([ceiling]) => accent.hue <= ceiling)?.[1] ?? UNKNOWN_LABEL
}

function greyName(lightness: number): string {
    if (lightness > GREY_PEARL_MIN_LIGHTNESS) return 'Pearl'
    return lightness < GREY_OBSIDIAN_MAX_LIGHTNESS ? 'Obsidian' : 'Steel'
}

function readAccent(hex?: string): Accent | null {
    const match = (hex ?? '').replace('#', '').match(HEX_TRIPLET)
    if (!match) return null
    const [red, green, blue] = [match[1], match[2], match[3]]
        .map((pair) => parseInt(pair, 16) / 255)
    const max = Math.max(red, green, blue)
    const min = Math.min(red, green, blue)
    const chroma = max - min
    return {
        chroma,
        lightness: (max + min) / 2,
        hue: chroma === 0 ? 0 : hueDegrees({ red, green, blue, max, chroma }),
    }
}

function hueDegrees({ red, green, blue, max, chroma }: Channels): number {
    let sextant: number
    if (max === red) sextant = ((green - blue) / chroma) % 6
    else if (max === green) sextant = (blue - red) / chroma + 2
    else sextant = (red - green) / chroma + 4
    const degrees = sextant * 60
    return degrees < 0 ? degrees + 360 : degrees
}
