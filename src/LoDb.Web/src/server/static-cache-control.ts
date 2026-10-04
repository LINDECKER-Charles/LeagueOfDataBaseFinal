import { isHashedAsset } from './hashed-asset';

const ONE_YEAR_SECONDS = 31_536_000;
const IMMUTABLE = `public, max-age=${ONE_YEAR_SECONDS}, immutable`;
// Same effect as `no-cache`, which Angular's HTTP transfer cache refuses to embed in the page:
// the browser would then fetch again the i18n catalogue the server rendered with.
const REVALIDATED = 'public, max-age=0, must-revalidate';

/**
 * Cache-Control of a static file, from its path relative to the browser output folder. A hashed
 * name changes with its content, so it is cached for a year; anything else (i18n catalogues,
 * files of public/, the service worker) is revalidated on every use.
 */
export function staticCacheControl(relativePath: string): string {
  return isHashedAsset(relativePath) ? IMMUTABLE : REVALIDATED;
}
