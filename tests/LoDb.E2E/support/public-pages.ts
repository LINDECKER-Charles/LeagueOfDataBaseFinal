/** The 21 locales of ADR 0005, in the order of the hreflang alternates. */
export const LOCALES = [
  'ar',
  'cs',
  'de',
  'el',
  'en',
  'es',
  'fr',
  'hu',
  'id',
  'it',
  'ja',
  'ko',
  'pl',
  'pt',
  'ro',
  'ru',
  'th',
  'tr',
  'vi',
  'zh-hans',
  'zh-hant',
] as const;

/**
 * What a page is to crawlers: `indexed` pages carry the SEO of ADR 0005; `provisional` ones
 * await their lot (5 and 6); `visitor` ones are private, rendered in the browser.
 */
export type PageKind = 'indexed' | 'provisional' | 'visitor';

export interface PublicPage {
  /** Path below `/{locale}/`: empty for the home. */
  readonly path: string;
  readonly kind: PageKind;
  /** JSON-LD types the page carries, besides the graph of the site. */
  readonly types: readonly string[];
}

const GAME_ENTITY = ['BreadcrumbList', 'VideoGame', 'Thing'];

function indexed(path: string, types: readonly string[] = []): PublicPage {
  return { path, kind: 'indexed', types };
}

/**
 * Every public page of the site, one per template: the catalogue in the latest version
 * (a pinned version is read from the API by the specs), the editorial pages, the pages of
 * later lots, and the pages a visitor sees before signing in.
 */
export const PUBLIC_PAGES: readonly PublicPage[] = [
  indexed(''),
  indexed('champions', ['ItemList']),
  indexed('items', ['ItemList']),
  indexed('runes', ['ItemList']),
  indexed('summoners', ['ItemList']),
  indexed('champions/Annie', ['BreadcrumbList', 'VideoGame', 'Person']),
  indexed('items/3031-infinity-edge', GAME_ENTITY),
  indexed('runes/8100-domination', GAME_ENTITY),
  indexed('summoners/SummonerFlash', GAME_ENTITY),
  indexed('about', ['AboutPage']),
  indexed('about/data', ['Dataset']),
  indexed('faq', ['FAQPage']),
  indexed('changelog', ['BreadcrumbList']),
  indexed('legal/notice'),
  indexed('legal/privacy'),
  indexed('legal/terms'),
  indexed('legal/cookies'),
  { path: 'trends', kind: 'provisional', types: [] },
  indexed('developers', ['BreadcrumbList']),
  { path: 'donate', kind: 'provisional', types: [] },
  { path: 'account/login', kind: 'visitor', types: [] },
  { path: 'account/register', kind: 'visitor', types: [] },
  { path: 'account/forgot-password', kind: 'visitor', types: [] },
];

/** The pages crawlers index. */
export const INDEXED_PAGES = PUBLIC_PAGES.filter((entry) => entry.kind === 'indexed');

/** The URL of a page in a locale, `/{locale}/{path}`. */
export function pageUrl(locale: string, path: string): string {
  return `/${locale}/${path}`;
}
