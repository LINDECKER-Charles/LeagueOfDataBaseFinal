import { isLocale } from '../../i18n/is-locale';
import { decodeSegment } from './decode-segment';
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
 * The router decodes a segment before matching it, so the locale and the version are read
 * decoded too (`/%66r/` is `/fr/`) and come out decoded; the page segments stay as the URL
 * encodes them. A first segment that is not a locale leaves the whole path to `page`.
 */
export function parsePublicUrl(url: string, isVersion: (segment: string) => boolean): PublicUrl {
  const { path, search, fragment } = splitUrl(url);
  const segments = path.split(SEGMENT_SEPARATOR).filter((segment) => segment !== '');
  const [first, second] = segments.slice(0, 2).map(decodeSegment);
  const locale = isLocale(first) ? first : null;
  const version =
    locale !== null && typeof second === 'string' && isVersion(second) ? second : null;
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
