import type { SeoUrls } from '../../urls/seo-urls';

/** What the site graph of a render is built from. */
export interface SiteGraphContext {
  readonly urls: SeoUrls;
  /** The document title of the page. */
  readonly pageName: string;
  /** The one-line pitch of the site (`base.description`), in the page's locale. */
  readonly siteDescription: string;
  /** BCP 47 tag of the page's locale. */
  readonly inLanguage: string;
}
