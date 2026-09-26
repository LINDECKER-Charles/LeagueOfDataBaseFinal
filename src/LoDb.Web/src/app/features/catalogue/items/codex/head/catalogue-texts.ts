/** The texts a catalogue page's head is written with, in the page's locale. */
export interface CatalogueTexts {
  /** A text of the SEO catalogue (`public/i18n/seo/`). */
  seo(key: string, params?: Readonly<Record<string, unknown>>): string;
  /** A text of the locale's main catalogue, such as a navigation label. */
  main(key: string): string;
}
