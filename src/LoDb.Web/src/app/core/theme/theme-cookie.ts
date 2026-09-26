/**
 * Written by the theme picker (L3.2) and read only in the browser: the SSR server never reads
 * a cookie, so the rendered HTML never depends on the theme and stays publicly cacheable.
 * Unsigned on purpose, as before: the value is checked against a closed list on read.
 */
export const THEME_COOKIE = 'lod_theme';
