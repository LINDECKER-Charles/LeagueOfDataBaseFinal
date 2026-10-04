/**
 * Cookie of the remembered context, named as before: the cookie policy page lists it, so
 * renaming it is a legal change. Written in the browser only: the SSR server never reads a
 * cookie, so the rendered HTML stays the same for everyone and publicly cacheable; nginx
 * reads its locale to answer `/`, which is never cached. Its value is
 * `loc=<locale>&l=<lang>&v=<version>`, unsigned and readable by scripts, unlike the legacy
 * one (signed, HttpOnly), which this format never parses.
 */
export const PREFERENCES_COOKIE = 'lod_prefs';
