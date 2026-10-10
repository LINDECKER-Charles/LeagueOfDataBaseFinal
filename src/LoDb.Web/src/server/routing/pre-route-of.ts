import { decodeSegment } from '../../app/core/routing/url/decode-segment';
import { parsePublicUrl } from '../../app/core/routing/url/parse-public-url';
import { QueryString } from '../../app/core/routing/url/query-string';
import { isVersionShaped } from './is-version-shaped';
import { ownedParamsOf } from './owned-params-of';
import type { PreRoute } from './pre-route';
import type { QueryLimits } from './query-limits';

const SEGMENT_SEPARATOR = '/';
const QUERY_START = '?';
// ADR 0005: every page lives under a locale, but the shared builds and the admin. The shape,
// not the list of locales: `/xx/…` is the app's to answer, with a 404 in its own frame.
const LOCALE_SHAPE = /^[a-z]{2}(?:-han[st])?$/;
const OUTSIDE_LOCALES: ReadonlySet<string> = new Set(['b', 'admin']);
// A slash or a NUL inside a segment: no page has one, and the router would read another path.
const UNSAFE_IN_SEGMENT = /[/\0]/;
// The renderer reads these otherwise than the classification here: it resolves the URL as a
// browser does (`.` and `..` segments, `\` as a slash), and its router takes `(…)` for outlets
// and `;name=value` for route parameters, `version` included. `/fr/x/../15.13.1/champions`,
// `/fr/(15.13.1/champions)` and `/fr/champions;version=15.13.1` would render a pinned page in
// the current lane. No page has such a segment.
const DOT_SEGMENTS: ReadonlySet<string> = new Set(['.', '..']);
const ROUTER_SYNTAX = /[\\();]/;

function splitTarget(url: string): { path: string; search: string } {
  const question = url.indexOf(QUERY_START);
  return question < 0
    ? { path: url, search: '' }
    : { path: url.slice(0, question), search: url.slice(question) };
}

// The path's segments decoded, as the router reads them, or null when one cannot be.
function decodedSegmentsOf(path: string): string[] | null {
  const decoded = path.split(SEGMENT_SEPARATOR).slice(1).map(decodeSegment);
  return decoded.every(isSafeSegment) ? decoded : null;
}

function isSafeSegment(segment: string | null): segment is string {
  return segment !== null && !UNSAFE_IN_SEGMENT.test(segment);
}

function isReadAsWritten(segment: string): boolean {
  return !DOT_SEGMENTS.has(segment) && !ROUTER_SYNTAX.test(segment);
}

function isInGrammar(first: string | undefined): boolean {
  return first !== undefined && (LOCALE_SHAPE.test(first) || OUTSIDE_LOCALES.has(first));
}

// The query a catalogue page keeps: the parameters it reads, once each, within bounds.
function cleanedSearchOf(path: string, search: string, limits: QueryLimits): string | null {
  const { locale, page } = parsePublicUrl(path, isVersionShaped);
  const decodedPage = page.map((segment) => decodeSegment(segment) ?? segment);
  const owned = locale === null ? null : ownedParamsOf(decodedPage);
  if (owned === null) {
    return null;
  }
  const isKept = (name: string, raw: string) =>
    owned.has(name) && raw.length <= limits.maxParamLength;
  return QueryString.parse(search).retain(isKept, limits.maxParams).toString();
}

/**
 * What the server answers a root-relative URL with before any render (ADR 0005), from its
 * shape alone: a 400 for a segment no decoding makes a path segment of (`%2F`, `%00`, a
 * malformed escape); the static 404 for a first segment outside the grammar; a 400 for a
 * segment of the grammar the renderer would read as another path (`..`, `\`, `(`, `;`); a
 * redirect to the same list or detail without the query parameters the page does not read,
 * so that a page has one URL and an invented parameter costs no render; else the render.
 */
export function preRouteOf(url: string, limits: QueryLimits): PreRoute {
  const { path, search } = splitTarget(url);
  const segments = decodedSegmentsOf(path);
  if (segments === null) {
    return { kind: 'bad-request' };
  }
  if (!isInGrammar(segments[0])) {
    return { kind: 'not-found' };
  }
  if (!segments.every(isReadAsWritten)) {
    return { kind: 'bad-request' };
  }
  const cleaned = cleanedSearchOf(path, search, limits);
  return cleaned === null || cleaned === search
    ? { kind: 'render' }
    : { kind: 'redirect', location: `${path}${cleaned}` };
}
