// The new form each URL of the former site must reach (docs/reecriture/plan-migration.md,
// table of the legacy 301). The verifier checks the shape of the Location, never an id: the
// API turns an old name into a canonical path from the catalogue, which moves with Data Dragon.

const VERSION = String.raw`\d+(?:\.\d+)+`;

// Old list segment → new list segment, and the shape of the detail id that follows it. The
// old detail segment is the singular (champion, object, rune, summoner).
const RESOURCES = {
  champions: { segment: 'champions', id: String.raw`[^/?#]+` },
  objects: { segment: 'items', id: String.raw`\d+(?:-[a-z0-9-]+)?` },
  runes: { segment: 'runes', id: String.raw`\d+(?:-[a-z0-9-]+)?` },
  summoners: { segment: 'summoners', id: String.raw`[^/?#]+` },
};

// Pages that keep their path under the locale.
const PAGES = /^\/(?:trends|about|about\/data|faq|changelog|developers|donate|legal\/[a-z-]+|u\/[^/]+)$/;

// Account pages, moved below /{locale}/account/ whatever their new name.
const ACCOUNT = /^\/(?:login|register|profile(?:\/.*)?|reset-password(?:\/.*)?|builds(?:\/.*)?)$/;

// Contracts kept as they are: never a redirect.
const CONTRACTS = /^\/(?:b\/|v1\/|webhooks\/stripe$|cdn\/blobs\/)/;

const UNKNOWN = { kind: 'unknown', description: 'not a former URL' };

function escape(text) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * The locale segment a former `?lang=` leads to: `en` without one; with one, a locale of the
 * same language (fr_FR → fr, zh_CN → zh-hans) or `en` when the site does not speak it.
 */
export function localePattern(lang) {
  const language = /^([a-z]{2})_[A-Z]{2}$/.exec(lang ?? '')?.[1];
  if (language === undefined || language === 'en') {
    return 'en';
  }
  return `(?:${language}(?:-[a-z]+)?|en)`;
}

function moved(pattern, lang, description) {
  return { kind: 'moved', pattern: new RegExp(`^${pattern}$`), lang, description };
}

// The resource and, for a detail, the name a path below the optional version names.
function catalogEntry(segments) {
  const [resource, name] = segments;
  if (segments.length === 1 && resource in RESOURCES) {
    return { resource, name: undefined };
  }
  const plural = `${resource}s`;
  if (segments.length === 2 && plural in RESOURCES && name !== '') {
    return { resource: plural, name };
  }
  return null;
}

// A list or a detail of the catalogue, pinned or not: the latest version drops its segment.
function catalog(locale, version, entry, lang) {
  const { segment, id } = RESOURCES[entry.resource];
  const pinned = version === undefined ? `(?:/${VERSION})?` : `(?:/${escape(version)})?`;
  const detail = entry.name !== undefined;
  const tail = detail ? `/${id}` : '';
  return moved(`/${locale}${pinned}/${segment}${tail}`, lang, `${segment} ${detail ? 'detail' : 'list'}`);
}

/**
 * What a former URL must answer. `path` is the path and query as the old site served them.
 *
 * - `moved`: a 301 whose Location matches `pattern` (its query holds at most the old `lang`),
 *   then a 200 there;
 * - `root`: `/`, a 302 to a locale, then a 200;
 * - `sitemap`: `/sitemap.xml`, served as it is (200);
 * - `contract`: kept, anything but a 301;
 * - `unknown`: no row of the table matches it.
 */
export function expectationOf(path) {
  const url = new URL(path, 'http://former.invalid');
  const lang = url.searchParams.get('lang') ?? undefined;
  const version = url.searchParams.get('version') ?? undefined;
  const locale = localePattern(lang);
  const pathname = url.pathname.length > 1 ? url.pathname.replace(/\/$/, '') : url.pathname;

  if (pathname === '/') {
    return { kind: 'root', pattern: /^\/[a-z]{2}(?:-[a-z]+)?\/$/, description: 'root' };
  }
  if (CONTRACTS.test(pathname)) {
    return { kind: 'contract', description: 'contract' };
  }
  if (pathname === '/sitemap.xml') {
    return { kind: 'sitemap', description: 'sitemap index' };
  }
  if (pathname === '/sitemaps/latest.xml') {
    return moved(String.raw`/sitemap\.xml`, undefined, 'latest sitemap');
  }
  const sitemap = new RegExp(`^/sitemaps/(${VERSION})\\.xml$`).exec(pathname);
  if (sitemap !== null) {
    return moved(`/sitemaps/en/(?:${escape(sitemap[1])}|latest)\\.xml`, undefined, 'version sitemap');
  }
  if (pathname === '/home') {
    return moved(`/${locale}/`, lang, 'home');
  }

  const segments = pathname.split('/').slice(1);
  const pinned = new RegExp(`^${VERSION}$`).test(segments[0]) ? segments[0] : undefined;
  const entry = catalogEntry(pinned === undefined ? segments : segments.slice(1));
  if (entry !== null) {
    return catalog(locale, pinned ?? version, entry, lang);
  }
  if (pinned !== undefined) {
    return UNKNOWN;
  }
  if (PAGES.test(pathname)) {
    return moved(`/${locale}${escape(pathname)}`, lang, 'page');
  }
  if (ACCOUNT.test(pathname)) {
    return moved(`/${locale}/account/[^?#]+`, lang, 'account page');
  }
  return UNKNOWN;
}

/**
 * Whether a Location answers the expectation: same origin, its path matches, and its query
 * holds nothing but the old `lang`, which a variant the locale cannot say keeps (en_GB under
 * /en/).
 */
export function locationMatches(expectation, location, base) {
  const target = new URL(location, base);
  if (target.origin !== new URL(base).origin || !expectation.pattern.test(target.pathname)) {
    return false;
  }
  const keys = [...target.searchParams.keys()];
  return (
    keys.length === 0 ||
    (keys.length === 1 && keys[0] === 'lang' && target.searchParams.get('lang') === expectation.lang)
  );
}
