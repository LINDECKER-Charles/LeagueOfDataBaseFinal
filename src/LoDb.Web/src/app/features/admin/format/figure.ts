/**
 * How a figure of the admin reads: `int` thousands-separated, `compact` (1.2k, 3M), `bytes`
 * in binary units, `euros` from cents, `pct` from a 0-100 share, `share` from a 0-1 one,
 * `ratio` as a multiplier (2.35×).
 */
export type FigureFormat = 'int' | 'compact' | 'bytes' | 'euros' | 'pct' | 'share' | 'ratio';

const BYTE_UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'] as const;
const BYTE_STEP = 1024;
const THOUSAND = 1_000;
const MILLION = 1_000_000;
const CENTS = 100;
const PERCENT = 100;
const RATIO_DECIMALS = 2;
const THOUSANDS = /\B(?=(\d{3})+(?!\d))/g;
// "1.0k" reads worse than "1k": a decimal that carries nothing is dropped.
const EMPTY_DECIMAL = /\.0$/;

function integer(value: number): string {
  return Math.round(value).toString().replace(THOUSANDS, ' ');
}

function compact(value: number): string {
  if (value >= MILLION) {
    return `${(value / MILLION).toFixed(1).replace(EMPTY_DECIMAL, '')}M`;
  }
  if (value >= THOUSAND) {
    return `${(value / THOUSAND).toFixed(1).replace(EMPTY_DECIMAL, '')}k`;
  }
  return String(Math.trunc(value));
}

function bytes(value: number): string {
  let size = value;
  let unit = 0;
  while (size >= BYTE_STEP && unit < BYTE_UNITS.length - 1) {
    size /= BYTE_STEP;
    unit++;
  }
  return `${unit === 0 ? Math.round(size) : size.toFixed(2)} ${BYTE_UNITS[unit]}`;
}

function euros(cents: number): string {
  const [units, decimals] = (cents / CENTS).toFixed(2).split('.');
  return `${units.replace(THOUSANDS, ' ')},${decimals} €`;
}

/**
 * Writes a figure the way the legacy admin did, so that a table and its chart never disagree
 * on a separator: a space between thousands, a point before decimals, euros in French.
 */
export function figure(value: number, format: FigureFormat = 'int'): string {
  switch (format) {
    case 'compact':
      return compact(value);
    case 'bytes':
      return bytes(value);
    case 'euros':
      return euros(value);
    case 'pct':
      return `${value.toFixed(1)} %`;
    case 'share':
      return `${(value * PERCENT).toFixed(1)} %`;
    case 'ratio':
      return `${value.toFixed(RATIO_DECIMALS)}×`;
    default:
      return integer(value);
  }
}
