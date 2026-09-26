/** The day an account was created, written out in the page's locale, `26 September 2026`. */
export function memberSince(createdAt: string, locale: string): string {
  // The API dates in UTC: the day stays the one the server knows, whatever the time zone.
  const format = new Intl.DateTimeFormat(locale, { dateStyle: 'long', timeZone: 'UTC' });
  return format.format(new Date(createdAt));
}
