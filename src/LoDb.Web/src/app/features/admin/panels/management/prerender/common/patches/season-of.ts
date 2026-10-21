/** The season of a patch: the first segment of its version, and the year it ran. */
export interface Season {
  /** The first segment as written, `16`: what groups the patches of a season. */
  readonly key: string;
  readonly major: number;
  /** 2026 for `16.x`; null for a segment outside the yearly seasons (`0.151.2`, `100.1`). */
  readonly year: number | null;
}

// Riot numbers its seasons by year since `3.x` in 2013: `4.x` is 2014, `16.x` is 2026. Data
// Dragon still lists the `0.x` betas, which `PatchVersion` accepts and no year fits.
const FIRST_YEARLY = 3;
const LAST_YEARLY = 99;
const YEAR_OFFSET = 2010;

/** The season of `version`, from its first segment. */
export function seasonOf(version: string): Season {
  const key = version.split('.')[0] ?? version;
  const major = Number(key);
  const isYearly = Number.isInteger(major) && major >= FIRST_YEARLY && major <= LAST_YEARLY;
  return { key, major, year: isYearly ? YEAR_OFFSET + major : null };
}
