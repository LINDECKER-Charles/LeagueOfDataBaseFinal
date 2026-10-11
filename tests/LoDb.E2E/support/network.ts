/**
 * The init script every page of the suite starts with (support/test.ts): a reader on a fast
 * connection, without Save-Data. Chromium otherwise derives `effectiveType` from the round
 * trips it measured, which differ from one machine to the next: below 4g, the page keeps a
 * clip's poster and defers the lists.
 */
export function fastConnection(): void {
  Object.defineProperty(Navigator.prototype, 'connection', {
    configurable: true,
    get: () => ({ saveData: false, effectiveType: '4g' }),
  });
}
