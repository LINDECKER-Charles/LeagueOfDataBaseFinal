import { decodeSegment } from '../../app/core/routing/url/decode-segment';
import { parsePublicUrl } from '../../app/core/routing/url/parse-public-url';
import { isVersionShaped } from '../routing/is-version-shaped';

// The page of what a patch changed, which has no page below it (VersionPages, in the API).
const PATCH_PAGE = 'changes';
// The four lists of a patch, the first segment of every entity page (CanonicalPath), and the
// patch page: the first segment of every page the pass stores.
const PAGE_TYPES: ReadonlySet<string> = new Set([
  'champions',
  'items',
  'runes',
  'summoners',
  PATCH_PAGE,
]);
// A list, or a list and one entity: `/fr/15.13.1/items/1036-long-sword` at most.
const MAX_PAGE_SEGMENTS = 2;
// A Data Dragon version as the site names it (`15.13.1`, `0.151.101`): never anything else
// that merely starts with a digit.
const VERSION = /^\d+(?:\.\d+){1,3}$/;
// The ids and slugs of every page (`MonkeyKing`, `SummonerFlash_Jade`, `1036-long-sword`).
const PAGE_SEGMENT = /^[A-Za-z0-9_-]+$/;
// Far over the longest page of the catalogue: a bound on what a client may make us keep.
const MAX_PATH_LENGTH = 160;

/**
 * The page of an older patch a refused render asked for, as `/{locale}/{version}/{page}`, its
 * patch page (`changes`) included: decoded as the router reads it, without its query, its
 * fragment nor a trailing slash, so that every spelling of one page counts once. Null for
 * anything else: a page of the latest patch, the home, a URL outside the page grammar. The API
 * still checks it against the catalogue of its version: this only keeps junk from filling the
 * table.
 */
export function refusedPagePathOf(url: string): string | null {
  const { locale, version, page } = parsePublicUrl(url, isVersionShaped);
  if (locale === null || version === null || !VERSION.test(version)) {
    return null;
  }
  if (page.length === 0 || page.length > MAX_PAGE_SEGMENTS) {
    return null;
  }
  const segments = page.map(decodeSegment);
  if (!isStoredPage(segments)) {
    return null;
  }
  const path = `/${locale}/${version}/${segments.join('/')}`;
  return path.length <= MAX_PATH_LENGTH ? path : null;
}

// A list, an entity below its list, or the patch page alone, every segment an id or a slug.
function isStoredPage(segments: readonly (string | null)[]): boolean {
  const isPage = segments.every((segment) => segment !== null && PAGE_SEGMENT.test(segment));
  const type = segments[0] as string;
  return isPage && PAGE_TYPES.has(type) && (type !== PATCH_PAGE || segments.length === 1);
}
