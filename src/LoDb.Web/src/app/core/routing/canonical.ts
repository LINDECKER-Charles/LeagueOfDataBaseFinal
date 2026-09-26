import { HttpStatusCode } from '@angular/common/http';
import type { CanonicalFacts } from './canonical-facts';
import type { CataloguePage } from './catalogue-page';
import { NOT_FOUND } from './outcome/not-found';
import type { PageOutcome } from './outcome/page-outcome';
import { QueryString } from './url/query-string';

const VERSION_PARAM = 'version';

function redirect(
  status: HttpStatusCode.MovedPermanently | HttpStatusCode.Found,
  location: string,
): PageOutcome {
  return { kind: 'redirect', status, location };
}

// `/{locale}/[{pinned}/]{resource}[/{entry}]{query}`: the page as requested.
function pathOf(page: CataloguePage): string {
  const segments = [page.locale, page.pinned, page.resource, page.entry].filter(
    (segment): segment is string => segment !== null,
  );
  return `/${segments.join('/')}${page.query}`;
}

// `/{locale}/[{pinned}/]{canonicalPath}{query}`: the page of the entity the id designates.
function entityPathOf(page: CataloguePage, canonicalPath: string): string {
  const version = page.pinned === null ? '' : `${page.pinned}/`;
  return `/${page.locale}/${version}${canonicalPath}${page.query}`;
}

// A `?version=` moves into the path, where the switcher writes it, so that a catalogue page
// has one URL per version. A version Data Dragon does not list is ignored, as any query is.
function queryVersionOutcome(page: CataloguePage, facts: CanonicalFacts): PageOutcome | null {
  const query = QueryString.parse(page.query);
  const requested = query.get(VERSION_PARAM);
  if (requested === null || !facts.versions.includes(requested)) {
    return null;
  }
  const pinned = requested === facts.latest ? null : requested;
  const rest = query.without(VERSION_PARAM).toString();
  return redirect(HttpStatusCode.MovedPermanently, pathOf({ ...page, pinned, query: rest }));
}

// The latest version is only ever served by its short URL; the slug is fixed on the way
// when the entity is already known, so the visitor lands in one hop.
function shortUrlOutcome(page: CataloguePage, facts: CanonicalFacts): PageOutcome {
  const short = { ...page, pinned: null };
  const { canonicalPath } = facts;
  const location =
    typeof canonicalPath === 'string' ? entityPathOf(short, canonicalPath) : pathOf(short);
  return redirect(HttpStatusCode.MovedPermanently, location);
}

function entryOutcome(page: CataloguePage, facts: CanonicalFacts): PageOutcome | null {
  const { canonicalPath } = facts;
  if (page.entry === null || canonicalPath === undefined) {
    return null;
  }
  if (canonicalPath === null) {
    const list = pathOf({ ...page, entry: null });
    return page.pinned === null ? NOT_FOUND : redirect(HttpStatusCode.Found, list);
  }
  const expected = entityPathOf(page, canonicalPath);
  return expected === pathOf(page) ? null : redirect(HttpStatusCode.MovedPermanently, expected);
}

/**
 * The canonical decision of ADR 0005 for a list or a detail of the catalogue, from
 * `/api/meta` and the detail's `canonicalPath`:
 *
 * - a wrong or missing slug gives a 301 to the canonical URL: the id decides, not the name;
 * - the latest version pinned in the path gives a 301 to the short URL;
 * - an entity missing from a pinned version gives a 302 to that version's list;
 * - missing without a pinned version, or on a version Data Dragon does not list: a 404;
 * - a known `?version=` gives a 301 to the same page with the version in its path.
 *
 * Redirects keep the query. Null means the URL is canonical and the page renders as is.
 */
export function canonical(page: CataloguePage, facts: CanonicalFacts): PageOutcome | null {
  if (page.pinned === null) {
    return queryVersionOutcome(page, facts) ?? entryOutcome(page, facts);
  }
  if (!facts.versions.includes(page.pinned)) {
    return NOT_FOUND;
  }
  if (page.pinned === facts.latest) {
    return shortUrlOutcome(page, facts);
  }
  return entryOutcome(page, facts);
}
