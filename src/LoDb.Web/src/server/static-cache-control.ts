// With `outputHashing: all`, Angular writes bundles at the browser root as `<name>-<hash>.js|css`
// and media under media/ as `<name>-<hash>.<ext>`. A hash is eight characters of `[\w-]`, the
// alphabet Angular's chunk optimizer itself matches: esbuild emits upper-case base32
// (`main-2ZNNMVSG.js`), rolldown mixed case with `-` and `_` (`chunk-C1xKWD6m.js`).
const HASHED_BUNDLE = /^[^/]+-[\w-]{8}\.(?:js|css)$/;
const HASHED_MEDIA = /^media\/[^/]+-[\w-]{8}\.\w+$/;
const ONE_YEAR_SECONDS = 31_536_000;
const IMMUTABLE = `public, max-age=${ONE_YEAR_SECONDS}, immutable`;
// Same effect as `no-cache`, which Angular's HTTP transfer cache refuses to embed in the page:
// the browser would then fetch again the i18n catalogue the server rendered with.
const REVALIDATED = 'public, max-age=0, must-revalidate';

/**
 * Cache-Control of a static file, from its path relative to the browser output folder. A hashed
 * name changes with its content, so it is cached for a year; anything else (i18n catalogues,
 * files of public/) is revalidated on every use. Files of public/ keep their own name, so
 * public/ holds no media/ folder and no root script or stylesheet named like a bundle.
 */
export function staticCacheControl(relativePath: string): string {
  const hashed = HASHED_BUNDLE.test(relativePath) || HASHED_MEDIA.test(relativePath);
  return hashed ? IMMUTABLE : REVALIDATED;
}
