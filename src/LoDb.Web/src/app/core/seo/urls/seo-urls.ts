/** The URLs a page's JSON-LD builders link to, all absolute. */
export interface SeoUrls {
  /** Canonical origin, without a trailing slash. */
  readonly origin: string;
  /** This page's canonical URL. */
  readonly canonical: string;
  /** Another page of the same locale and version, such as `champions` for a breadcrumb. */
  page(path: string): string;
  /** A root-relative URL made absolute, such as an image; an absolute one is kept. */
  absolute(url: string): string;
}
