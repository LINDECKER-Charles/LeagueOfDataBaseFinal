/**
 * Headers that keep a response out of search indexes: private pages, errors and shared
 * builds. The header covers what a `<meta name="robots">` cannot, such as a redirect body
 * or a page a crawler reads before its scripts run.
 */
export const NOINDEX_HEADERS: Readonly<Record<string, string>> = { 'X-Robots-Tag': 'noindex' };
