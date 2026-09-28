import type { DonutSlice } from './scale/donut-slice';

/** The vocabularies whose categories keep one colour wherever they are drawn. */
export type Vocabulary = 'resource' | 'device' | 'source' | 'plan';

/** A count of the API under its key: `champion`, `mobile`, `search`, `monthly`. */
interface Counted {
  readonly name: string;
  readonly value: number;
}

// The legacy series (--series-1 to -5 were gold, blue, green, red, cyan), by category.
const COLORS: Readonly<Record<Vocabulary, Readonly<Record<string, string>>>> = {
  resource: {
    champion: 'var(--color-gold)',
    item: 'var(--color-series-blue)',
    runesReforged: 'var(--color-series-green)',
    summoner: 'var(--color-series-red)',
    home: 'var(--color-series-cyan)',
  },
  device: {
    desktop: 'var(--color-gold)',
    mobile: 'var(--color-series-blue)',
    tablet: 'var(--color-series-green)',
    other: 'var(--color-text-muted)',
  },
  source: {
    direct: 'var(--color-gold)',
    search: 'var(--color-series-cyan)',
    social: 'var(--color-series-blue)',
    external: 'var(--color-series-red)',
    internal: 'var(--color-series-green)',
  },
  plan: {
    free: 'var(--color-gold)',
    credits: 'var(--color-series-cyan)',
    monthly: 'var(--color-series-blue)',
    monthly_plus: 'var(--color-series-green)',
    annual: 'var(--color-series-red)',
    annual_plus: 'var(--color-gold-deep)',
  },
};
// A category the admin does not know yet.
const UNKNOWN = 'var(--color-text-muted)';

/**
 * The slices of a donut of `vocabulary`, each in the fixed colour of its category, as the
 * legacy admin tinted them: "Champions" is gold on every page, whatever its rank. `label`
 * names a category from its key.
 */
export function categorySlices(
  vocabulary: Vocabulary,
  rows: readonly Counted[],
  label: (key: string) => string,
): DonutSlice[] {
  const colors = COLORS[vocabulary];
  return rows.map((row) => ({
    name: label(row.name),
    value: row.value,
    color: colors[row.name] ?? UNKNOWN,
  }));
}
