const PERCENT = 100;

/** A count of requests in the locale's digits and grouping: `12 345`, `12,345`. */
export function formatCount(locale: string, count: number): string {
  return new Intl.NumberFormat(locale).format(count);
}

/** A UTC day of the API (`2026-09-27`) as the locale writes a short date. */
export function formatDay(locale: string, day: string): string {
  return formatDate(locale, `${day}T00:00:00Z`);
}

/** An instant of the API as the locale writes its UTC day. */
export function formatDate(locale: string, instant: string): string {
  const date = new Date(instant);
  return Number.isNaN(date.getTime())
    ? instant
    : new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeZone: 'UTC' }).format(date);
}

/** Share of the quota used, whole percent from 0 to 100; a plan without quota reads full. */
export function quotaPercent(used: number, quota: number): number {
  return quota > 0 ? Math.min(PERCENT, Math.round((used / quota) * PERCENT)) : PERCENT;
}
