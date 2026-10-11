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
