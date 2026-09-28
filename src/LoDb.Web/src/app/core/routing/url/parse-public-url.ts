import { isLocale } from '../../i18n/is-locale';
import type { PublicUrl } from './public-url';
import { QueryString } from './query-string';

const SEGMENT_SEPARATOR = '/';

// Splits `path?query#fragment` at the first `#`, then at the first `?` before it.
function splitUrl(url: string): { path: string; search: string; fragment: string } {
  const hash = url.indexOf('#');
  const beforeHash = hash < 0 ? url : url.slice(0, hash);
  const question = beforeHash.indexOf('?');
  return {
    path: question < 0 ? beforeHash : beforeHash.slice(0, question),
    search: question < 0 ? '' : beforeHash.slice(question),
    fragment: hash < 0 ? '' : url.slice(hash),
  };
}

/**
 * Cuts a root-relative URL along the grammar of ADR 0005. `isVersion` tells a version
 * segment from a page segment; it comes from the pattern of `/api/meta`, never a copy.
 * A first segment that is not a locale leaves the whole path to `page`.
 */
export function parsePublicUrl(url: string, isVersion: (segment: string) => boolean): PublicUrl {
  const { path, search, fragment } = splitUrl(url);
  const segments = path.split(SEGMENT_SEPARATOR).filter((segment) => segment !== '');
  const [first, second] = segments;
  const locale = isLocale(first) ? first : null;
  const version = locale !== null && second !== undefined && isVersion(second) ? second : null;
  const skipped = (locale === null ? 0 : 1) + (version === null ? 0 : 1);
  return {
    locale,
    version,
    page: segments.slice(skipped),
    query: QueryString.parse(search),
    fragment,
    trailingSlash: path.length > 1 && path.endsWith(SEGMENT_SEPARATOR),
  };
}
