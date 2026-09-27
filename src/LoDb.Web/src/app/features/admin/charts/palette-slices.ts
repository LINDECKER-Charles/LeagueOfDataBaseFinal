import type { DonutSlice } from './scale/donut-slice';

// The colours of the slices, in order: tokens of the design system, never literals.
const PALETTE = [
  'var(--color-gold)',
  'var(--color-hex)',
  'var(--color-gold-rich)',
  'var(--color-danger-light)',
  'var(--color-gold-light)',
  'var(--color-text-dim)',
] as const;

/** A named count, as the reports rank them. */
interface Counted {
  readonly name: string;
  readonly value: number;
}

/**
 * The slices of a donut, coloured in turn from the palette. Past as many rows as it has
 * colours, the smallest share the last slice, named `rest`.
 */
export function paletteSlices(rows: readonly Counted[], rest: string): DonutSlice[] {
  const kept = rows.length > PALETTE.length ? rows.slice(0, PALETTE.length - 1) : rows;
  const others = rows.slice(kept.length).reduce((sum, row) => sum + row.value, 0);
  const named = others > 0 ? [...kept, { name: rest, value: others }] : kept;
  return named.map((row, index) => ({
    name: row.name,
    value: row.value,
    color: PALETTE[index % PALETTE.length] ?? PALETTE[0],
  }));
}
