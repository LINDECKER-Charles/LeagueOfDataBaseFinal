/** The translations the head of the trends reads, in the page's locale. */
export interface TrendsTexts {
  /** A key of the `seo` scope, such as `trends.title`. */
  readonly seo: (key: string) => string;
  /** A key of the root catalogue, such as `community.trends.title`. */
  readonly main: (key: string) => string;
}
