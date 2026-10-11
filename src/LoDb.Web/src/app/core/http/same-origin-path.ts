/**
 * The path of `url` relative to the page's origin (`i18n/fr.json`), or null for any other
 * origin, the API's included. Relative URLs never get here: Angular makes them absolute
 * against the page before the backend sees them.
 */
export function sameOriginPath(url: string, pageOrigin: string): string | null {
  let target: URL;
  try {
    target = new URL(url);
  } catch {
    return null;
  }
  return target.origin === pageOrigin ? target.pathname.slice(1) : null;
}
