/** How an instant reads: its day, down to the minute, or down to the second. */
export type StampFormat = 'date' | 'minute' | 'second';

const PARTS: Readonly<Record<StampFormat, Intl.DateTimeFormatOptions>> = {
  date: { day: '2-digit', month: '2-digit', year: 'numeric' },
  minute: { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' },
  second: {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  },
};

// Built once: a formatter is costly to create and a table writes one stamp per row.
const FORMATTERS = Object.fromEntries(
  Object.entries(PARTS).map(([format, parts]) => [
    format,
    new Intl.DateTimeFormat('fr-FR', { ...parts, timeZone: 'UTC' }),
  ]),
) as Readonly<Record<StampFormat, Intl.DateTimeFormat>>;

/**
 * Writes an ISO instant of the API in French, in UTC as the audit journal states it, so that
 * every page of the admin dates an event the same way. A missing instant reads as a dash.
 */
export function stamp(iso: string | null | undefined, format: StampFormat = 'date'): string {
  const time = iso ? Date.parse(iso) : Number.NaN;
  return Number.isNaN(time) ? '—' : FORMATTERS[format].format(time).replace(',', '');
}
