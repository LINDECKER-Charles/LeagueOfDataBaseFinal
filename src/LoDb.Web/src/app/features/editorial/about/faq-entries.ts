/**
 * The FAQ entries, in display order. This list is the contract; the copy lives in the
 * `about` scope under `faq.<id>.question` and `faq.<id>.answer`. The page and its FAQPage
 * node both read it, so the answers shown and the ones given to crawlers cannot drift.
 */
export const FAQ_ENTRIES = [
  'what-is-it',
  'data-source',
  'freshness',
  'languages',
  'old-patches',
  'free',
  'account',
  'builds',
  'api',
  'offline',
  'riot-affiliation',
] as const;
