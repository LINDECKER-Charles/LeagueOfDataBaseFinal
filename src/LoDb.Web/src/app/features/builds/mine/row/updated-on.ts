/**
 * The day a build last changed, as its row prints it in the page's locale (`26/09/2026` in
 * French). Read in UTC, the zone the API stamps in, so the day never shifts with the reader.
 */
export function updatedOn(stamp: string, locale: string): string {
  const date = new Date(stamp);
  if (Number.isNaN(date.getTime())) {
    return stamp;
  }
  return new Intl.DateTimeFormat(locale, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(date);
}
