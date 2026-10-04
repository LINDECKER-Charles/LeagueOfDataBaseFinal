/** Falls back to the Hextech gold token so an accent-less chroma still reads. */
const FALLBACK_ACCENT = 'var(--color-gold)'

/** Two-stop diagonal built from a chroma's accent colours (swatch ring / chip). */
export function chromaRamp(colors: readonly string[]): string {
    const from = colors[0] ?? FALLBACK_ACCENT
    const to = colors[1] ?? from
    return `linear-gradient(135deg, ${from}, ${to})`
}
