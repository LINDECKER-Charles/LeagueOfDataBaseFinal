/**
 * How a figure of the admin reads: `int` thousands-separated, `compact` (1.2k, 3M), `bytes`
 * in binary units, `money` an amount from cents in French (1 234,50), `euros` the same in
 * euros, `pct` from a 0-100 share, `share` from a 0-1 one (99.5 %, but 100 %), `percent` the
 * same rounded to a whole percent, `ratio` as a multiplier (2.35×).
 */
export type FigureFormat =
  'int' | 'compact' | 'bytes' | 'money' | 'euros' | 'pct' | 'share' | 'percent' | 'ratio';

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

function money(cents: number): string {
  const [units, decimals] = (cents / CENTS).toFixed(2).split('.');
  return `${units.replace(THOUSANDS, ' ')},${decimals}`;
}

const FORMATTERS: Readonly<Record<FigureFormat, (value: number) => string>> = {
  int: integer,
  compact,
  bytes,
  money,
  euros: (cents) => `${money(cents)} €`,
  pct: (value) => `${value.toFixed(1)} %`,
  share: (value) => `${(value * PERCENT).toFixed(1).replace(EMPTY_DECIMAL, '')} %`,
  percent: (value) => `${Math.round(value * PERCENT)} %`,
  ratio: (value) => `${value.toFixed(RATIO_DECIMALS)}×`,
};

/**
 * Writes a figure the way the legacy admin did, so that a table and its chart never disagree
 * on a separator: a space between thousands, a point before decimals, euros in French.
 */
export function figure(value: number, format: FigureFormat = 'int'): string {
  return FORMATTERS[format](value);
}
