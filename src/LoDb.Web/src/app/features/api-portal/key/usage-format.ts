const PERCENT = 100;
const THOUSANDS = /\B(?=(\d{3})+(?!\d))/g;
// No-break: a count never wraps, and between digits it stays in their order in RTL.
const GROUP_SEPARATOR = '\u00a0';

function twoDigits(value: number): string {
  return String(value).padStart(2, '0');
}

/**
 * A count of requests grouped by thousands with a space, `12 345`, in every locale, as the
 * legacy portal wrote it (`number_format(0, '.', ' ')`).
 */
export function formatCount(count: number): string {
  return String(Math.trunc(count)).replace(THOUSANDS, GROUP_SEPARATOR);
}

/** A UTC day of the API (`2026-09-27`) as the legacy portal wrote it, `27/09/2026`. */
export function formatDay(day: string): string {
  return formatDate(`${day}T00:00:00Z`);
}

/**
 * The UTC day of an instant of the API, `dd/mm/yyyy` in every locale, as the legacy portal
 * wrote it (`date('d/m/Y')`); what it cannot read is kept as it came.
 */
export function formatDate(instant: string): string {
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) {
    return instant;
  }
  const day = twoDigits(date.getUTCDate());
  return `${day}/${twoDigits(date.getUTCMonth() + 1)}/${date.getUTCFullYear()}`;
}

/** Share of the quota used, whole percent from 0 to 100; a plan without quota reads full. */
export function quotaPercent(used: number, quota: number): number {
  return quota > 0 ? Math.min(PERCENT, Math.round((used / quota) * PERCENT)) : PERCENT;
}
