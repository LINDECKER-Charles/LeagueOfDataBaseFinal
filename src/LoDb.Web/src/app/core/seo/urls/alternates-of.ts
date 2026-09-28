import { DEFAULT_LOCALE } from '../../i18n/default-locale';
import { LOCALES } from '../../i18n/locales';
import { hreflangOf } from './hreflang-of';
import type { PageAddress } from './page-address';
import { seoUrlsOf } from './seo-urls-of';

const X_DEFAULT = 'x-default';

/**
 * The `hreflang` alternates of a page: the same page in each of the 21 locales, then
 * `x-default`. The home's default is `/`, which picks a locale from `Accept-Language`; any
 * other page's is its `en` version, the language crawlers have always seen.
 */
export function alternatesOf(
  address: PageAddress,
): readonly { readonly hreflang: string; readonly href: string }[] {
  const inLocales = LOCALES.map((locale) => ({
    hreflang: hreflangOf(locale),
    href: seoUrlsOf({ ...address, locale }).canonical,
  }));
  const isHome = address.path === '' && address.version === null;
  const fallback = isHome
    ? `${address.origin}/`
    : seoUrlsOf({ ...address, locale: DEFAULT_LOCALE }).canonical;
  return [...inLocales, { hreflang: X_DEFAULT, href: fallback }];
}
