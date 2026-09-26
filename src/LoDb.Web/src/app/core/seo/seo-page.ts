import type { Locale } from '../i18n/locales';
import type { JsonLdNode } from './json-ld/core/json-ld-node';
import type { SeoUrls } from './urls/seo-urls';

interface SeoPageBase {
  /** The page's own title, translated, without the site name. */
  readonly title: string;
  /** The translated meta description; the site's pitch (`base.description`) by default. */
  readonly description?: string;
  /**
   * `site`: "{title} — League Of Data Base", with the patch on a pinned version (default);
   * `account`: "{title} · {base.title}" (default of private pages, the API portal too);
   * `admin`: "{title} · Admin · LODB"; `raw`: the title as given (home).
   */
  readonly titleFormat?: 'site' | 'account' | 'admin' | 'raw';
  /** The page's locale; the active one by default. */
  readonly locale?: Locale;
  /** Open Graph image, root-relative or absolute; `/preview/home.png` by default. */
  readonly image?: string | null;
  /** `profile` on a public profile; `website` by default. */
  readonly ogType?: 'website' | 'profile';
}

interface AddressedPage extends SeoPageBase {
  /** `page`: indexable (default); `donation-return`: `noindex, follow`, no alternates. */
  readonly kind?: 'page' | 'donation-return';
  /** Path below `/{locale}/[{version}/]`: the API's `canonicalPath`, `''` for home. */
  readonly path: string;
  /** A pinned version older than the latest: the page is then self-canonical. */
  readonly version?: string | null;
  /** The page's own nodes, after the site graph; built once the URLs are known. */
  readonly jsonLd?: (urls: SeoUrls) => readonly JsonLdNode[];
}

interface UnlistedPage extends SeoPageBase {
  /**
   * Never indexed, never canonical, no structured data: account and admin pages, errors,
   * shared builds (`/b/*`, whose Open Graph tags still make the link previews).
   */
  readonly kind: 'private' | 'error' | 'share';
}

/**
 * What a page hands to {@link Seo.apply}: its texts, translated, and where it lives. The
 * service derives the rest (title pattern, canonical, alternates, robots, Open Graph).
 */
export type SeoPage = AddressedPage | UnlistedPage;
