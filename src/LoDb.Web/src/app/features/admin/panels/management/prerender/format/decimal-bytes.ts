const UNITS = ['B', 'kB', 'MB', 'GB', 'TB', 'PB'] as const;
// The store bills a GB of 10^9 bytes: a volume read in 1024 steps would not match its price.
const STEP = 1_000;
const DECIMALS = 2;

/**
 * A volume in decimal units, `9.26 GB` for 9 264 000 000 bytes: the unit the prices of the
 * store are quoted in, so that a volume times its price gives the cost the panel shows.
 */
export function decimalBytes(value: number): string {
  let size = value;
  let unit = 0;
  while (size >= STEP && unit < UNITS.length - 1) {
    size /= STEP;
    unit++;
  }
  return `${unit === 0 ? Math.round(size) : size.toFixed(DECIMALS)} ${UNITS[unit]}`;
}
