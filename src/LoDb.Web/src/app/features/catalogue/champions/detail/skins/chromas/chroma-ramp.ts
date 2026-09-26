/** An accent-less chroma still reads, in the Hextech gold. */
const FALLBACK_ACCENT = 'var(--color-gold)';

/** The two-stop diagonal of a chroma's accent colours, behind its swatch. */
export function chromaRamp(colors: readonly string[]): string {
  const from = colors[0] ?? FALLBACK_ACCENT;
  const to = colors[1] ?? from;
  return `linear-gradient(135deg, ${from}, ${to})`;
}
