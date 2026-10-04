import type { PageAddress } from './page-address';
import type { SeoUrls } from './seo-urls';

const ABSOLUTE_URL = /^https?:\/\//i;
const LEADING_SLASHES = /^\/+/;

function pageUrl(address: PageAddress, path: string): string {
  const version = address.version === null ? '' : `${address.version}/`;
  return `${address.origin}/${address.locale}/${version}${path.replace(LEADING_SLASHES, '')}`;
}

/**
 * The URLs of a page. The canonical is built from the address alone, never from the
 * current URL: a query string or a stale slug can never leak into it.
 */
export function seoUrlsOf(address: PageAddress): SeoUrls {
  return {
    origin: address.origin,
    canonical: pageUrl(address, address.path),
    page: (path) => pageUrl(address, path),
    absolute: (url) =>
      ABSOLUTE_URL.test(url) ? url : `${address.origin}/${url.replace(LEADING_SLASHES, '')}`,
  };
}
