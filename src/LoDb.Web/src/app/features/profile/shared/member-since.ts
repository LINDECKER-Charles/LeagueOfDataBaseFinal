/** The month an account was created, written out in the page's locale, `September 2026`. */
export function memberSince(createdAt: string, locale: string): string {
  // The API dates in UTC: the month stays the one the server knows, whatever the time zone,
  // and the server's render agrees with the browser's.
  const format = new Intl.DateTimeFormat(locale, {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  });
  return format.format(new Date(createdAt));
}
