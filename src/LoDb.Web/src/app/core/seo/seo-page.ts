import type { CanonicalLink } from '../api/generated/models/canonical-link';
import type { Locale } from '../i18n/locales';
import type { SeoImage } from './images/seo-image';
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
  /**
   * Open Graph image: a URL, root-relative or absolute, or the image with its size and what
   * it shows; the home's preview card by default.
   */
  readonly image?: string | SeoImage | null;
  /** `profile` on a public profile; `website` by default. */
  readonly ogType?: 'website' | 'profile';
}

interface AddressedPage extends SeoPageBase {
  /**
   * `page`: indexable (default); `noindex`: `noindex, follow`, its links still followed, but
   * neither alternates nor structured data (a donation's return, a patch that changed nothing).
   */
  readonly kind?: 'page' | 'noindex';
  /** Path below `/{locale}/[{version}/]`: the API's `canonicalPath`, `''` for home. */
  readonly path: string;
  /** A pinned version, other than the latest one: see {@link canonical}. */
  readonly version?: string | null;
  /**
   * Where a page of a pinned version is indexed (ADR 0005, amendment of 2026-10-22): an
   * entity's page hands its API `canonical` over, which always names a page; unset, the same
   * path at the latest version. A page of the latest version is its own canonical whatever
   * this says.
   */
  readonly canonical?: CanonicalLink;
  /**
   * The patches a pinned page's title names after it: by default its own version
   * (`seo.versioned_suffix`); the run of patches of a state whose opener it is
   * (`seo.state_range_suffix`, or the version suffix for a run of one); `false` when the
   * title already names its patch. A title never carries two of them.
   */
  readonly titlePatches?: { readonly first: string; readonly last: string } | false;
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
