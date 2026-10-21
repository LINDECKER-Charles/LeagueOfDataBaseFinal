// The colour of each cell code, of a coverage (c w p f -) or of a pass (. r z c w f i _): the
// tones of the matrix, flat, since a strip has no room for a corner or a hatch.
const COLOURS: Readonly<Record<string, string>> = {
  c: 'var(--color-good)',
  w: 'color-mix(in srgb, var(--color-good) 65%, var(--color-bad))',
  p: 'var(--color-gold)',
  f: 'var(--color-bad)',
  '-': 'var(--color-track)',
  '.': 'var(--color-track)',
  r: 'var(--color-hex)',
  z: 'color-mix(in srgb, var(--color-hex) 45%, transparent)',
  i: 'color-mix(in srgb, var(--color-gold) 60%, transparent)',
  _: 'transparent',
};
const PERCENT = 100;
const DECIMALS = 3;

function stop(position: number): string {
  return `${Number(position.toFixed(DECIMALS))}%`;
}

/**
 * The background of a strip of cells, one element whatever their number (§ 4.4): a gradient
 * with one hard stop per cell, from the left. The strip mirrors itself in a right-to-left
 * locale. An unknown code paints nothing, like a locale outside the pass.
 */
export function stripGradient(cells: string): string {
  const codes = [...cells];
  if (codes.length === 0) {
    return 'none';
  }
  const width = PERCENT / codes.length;
  const stops = codes.map((code, index) => {
    const colour = COLOURS[code] ?? COLOURS['_'];
    return `${colour} ${stop(index * width)} ${stop((index + 1) * width)}`;
  });
  return `linear-gradient(to right, ${stops.join(', ')})`;
}
