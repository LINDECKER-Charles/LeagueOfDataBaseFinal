/** The translated texts of the site the head needs, in the page's locale. */
export interface SeoTexts {
  /** `base.title`, closing the account pages' titles. */
  readonly siteTitle: string;
  /** `base.description`, the description of a page that has none. */
  readonly siteDescription: string;
  /**
   * The patches a pinned page's title names (`seo.versioned_suffix`, or
   * `seo.state_range_suffix` on a closed state's opener); null on a latest page, or one whose
   * title names its patch itself.
   */
  readonly versionedSuffix: string | null;
}
