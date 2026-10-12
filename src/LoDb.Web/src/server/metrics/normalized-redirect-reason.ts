/**
 * Why the server sent a page elsewhere, the `reason` of `lodb_ssr_normalized_redirects_total`:
 * `query` for the pre-router's redirect to a catalogue URL without the parameters the page does
 * not read (no render), `slash` for its redirect of a page to its one path (`/fr` to the home's
 * `/fr/`, a trailing slash dropped everywhere else, no render), `canonical` for a page whose
 * render answered a permanent redirect to its canonical URL (a slug, the latest patch pinned in
 * the path, a `?version=`).
 */
export type NormalizedRedirectReason = 'query' | 'slash' | 'canonical';
