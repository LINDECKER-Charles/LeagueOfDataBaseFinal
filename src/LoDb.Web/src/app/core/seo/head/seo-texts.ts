/** The translated texts of the site the head needs, in the page's locale. */
export interface SeoTexts {
  /** `base.title`, closing the account pages' titles. */
  readonly siteTitle: string;
  /** `base.description`, the description of a page that has none. */
  readonly siteDescription: string;
  /** `seo.versioned_suffix` for the page's pinned version; null on a latest page. */
  readonly versionedSuffix: string | null;
}
