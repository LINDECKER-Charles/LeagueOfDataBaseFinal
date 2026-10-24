import type { Tone } from '../../../../widgets/badge';

/** The categories of the journal, in the order the API lists them. */
export const ROBOT_CATEGORIES = [
  'search',
  'ai',
  'assistant',
  'seo',
  'social',
  'monitoring',
  'archive',
  'tool',
  'unidentified',
  'undeclared',
] as const;

/** The verdicts of the reverse DNS, in the order the filters list them. */
export const ROBOT_VERDICTS = ['verified', 'mismatch', 'unresolved', 'unverifiable'] as const;

/** The sections of a request, in the order the filters list them. */
export const ROBOT_SECTIONS = [
  'home',
  'list',
  'detail',
  'archive',
  'page',
  'sitemap',
  'robots',
  'api',
  'asset',
  'other',
] as const;

/** The classes of statuses a chart draws, by their first digit. */
export const STATUS_CLASSES = [2, 3, 4, 5] as const;

// One colour per category wherever it is drawn: the engines in gold, the AI in red.
const CATEGORY_COLORS: Readonly<Record<string, string>> = {
  search: 'var(--color-gold)',
  ai: 'var(--color-series-red)',
  assistant: 'var(--color-series-cyan)',
  seo: 'var(--color-series-blue)',
  social: 'var(--color-series-green)',
  monitoring: 'var(--color-hex)',
  archive: 'var(--color-gold-deep)',
  tool: 'var(--color-text-muted)',
  unidentified: 'var(--color-gold-pale)',
  undeclared: 'var(--color-danger-light)',
};
const STATUS_COLORS: Readonly<Record<string, string>> = {
  '2xx': 'var(--color-good)',
  '3xx': 'var(--color-series-blue)',
  '4xx': 'var(--color-gold)',
  '5xx': 'var(--color-bad)',
};
const VERDICT_TONES: Readonly<Record<string, Tone>> = {
  verified: 'good',
  mismatch: 'bad',
  unresolved: 'warn',
  unverifiable: 'muted',
};
const UNKNOWN = 'var(--color-text-muted)';
const STATUS_CLASS_WIDTH = 100;
const CLIENT_ERROR = 4;
const SERVER_ERROR = 5;

export function categoryColor(category: string): string {
  return CATEGORY_COLORS[category] ?? UNKNOWN;
}

/** The colour of a class of statuses: `2xx`… */
export function statusColor(statusClass: string): string {
  return STATUS_COLORS[statusClass] ?? UNKNOWN;
}

export function verdictTone(verdict: string): Tone {
  return VERDICT_TONES[verdict] ?? 'muted';
}

/** A success reads good, a refusal or a missing page warns, a failure of the server is bad. */
export function statusTone(status: number): Tone {
  const statusClass = Math.floor(status / STATUS_CLASS_WIDTH);
  if (statusClass === SERVER_ERROR) {
    return 'bad';
  }
  return statusClass === CLIENT_ERROR ? 'warn' : 'good';
}
