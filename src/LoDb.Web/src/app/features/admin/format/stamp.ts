/**
 * How an instant reads: in French, its day, down to the minute or down to the second; or raw,
 * as the legacy printed some of them: its ISO day (`day-iso`), or its ISO second tagged with
 * the zone (`utc`), for a stamp no heading says is in UTC.
 */
export type StampFormat = 'date' | 'minute' | 'second' | 'day-iso' | 'utc';

const DAY: Intl.DateTimeFormatOptions = { day: '2-digit', month: '2-digit', year: 'numeric' };
const MINUTE: Intl.DateTimeFormatOptions = { ...DAY, hour: '2-digit', minute: '2-digit' };
// `2026-09-28T00:01:24.000Z`: the day ends at 10, the second at 19.
const ISO_DAY_END = 10;
const ISO_SECOND_END = 19;

function french(parts: Intl.DateTimeFormatOptions): (time: number) => string {
  // Built once: a formatter is costly to create and a table writes one stamp per row.
  const formatter = new Intl.DateTimeFormat('fr-FR', { ...parts, timeZone: 'UTC' });
  return (time) => formatter.format(time).replace(',', '');
}

function isoPrefix(time: number, end: number): string {
  return new Date(time).toISOString().slice(0, end).replace('T', ' ');
}

const WRITERS: Readonly<Record<StampFormat, (time: number) => string>> = {
  date: french(DAY),
  minute: french(MINUTE),
  second: french({ ...MINUTE, second: '2-digit' }),
  'day-iso': (time) => isoPrefix(time, ISO_DAY_END),
  utc: (time) => `${isoPrefix(time, ISO_SECOND_END)} UTC`,
};

/**
 * Writes an ISO instant of the API in UTC, as the audit journal states it, so that every page
 * of the admin dates an event the same way, whatever offset the API wrote it with. A missing
 * instant reads as a dash.
 */
export function stamp(iso: string | null | undefined, format: StampFormat = 'date'): string {
  const time = iso ? Date.parse(iso) : Number.NaN;
  return Number.isNaN(time) ? '—' : WRITERS[format](time);
}
