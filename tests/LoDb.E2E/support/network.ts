/**
 * An init script (`page.addInitScript(saveData)`) that has the page see a reader who turned
 * Save-Data on, as Chromium reports it through `navigator.connection`: the page then fetches
 * only what the reader asks for.
 */
export function saveData(): void {
  Object.defineProperty(Navigator.prototype, 'connection', {
    configurable: true,
    get: () => ({ saveData: true, effectiveType: '4g' }),
  });
}

/**
 * The init script every page of the suite starts with (support/test.ts): a reader on a fast
 * connection, without Save-Data. Chromium otherwise derives `effectiveType` from the round
 * trips it measured, which differ from one machine to the next: below 4g, the page keeps a
 * clip's poster and defers the lists. A spec that adds `saveData` after it overrides it.
 */
export function fastConnection(): void {
  Object.defineProperty(Navigator.prototype, 'connection', {
    configurable: true,
    get: () => ({ saveData: false, effectiveType: '4g' }),
  });
}
