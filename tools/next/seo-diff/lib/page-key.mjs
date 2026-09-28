// A page's identity, whatever the URL grammar that names it: the legacy site's
// (`/champion/Annie`, `/16.14.1/object/3031`) or ADR 0005's (`/en/items/3031-infinity-edge`).
// Two canonicals are equivalent when their keys are equal: kind, resource, id, version. The
// locale is not part of the key: the legacy URLs carry none.

const VERSION = /^\d+\.\d+\.\d+$/;
const LATEST = 'latest';
// The five rune paths: the legacy site names them, ADR 0005 numbers them (Riot's ids).
const RUNE_PATH_IDS = {
  precision: '8000',
  domination: '8100',
  sorcery: '8200',
  inspiration: '8300',
  resolve: '8400',
};
const LEGACY_LISTS = {
  champions: 'champions',
  objects: 'items',
  runes: 'runes',
  summoners: 'summoners',
};
const LEGACY_DETAILS = {
  champion: 'champions',
  object: 'items',
  rune: 'runes',
  summoner: 'summoners',
};
const NEXT_RESOURCES = new Set(['champions', 'items', 'runes', 'summoners']);
// Resources whose ids come first in a decorated segment: `3031-infinity-edge`.
const SLUGGED = new Set(['items', 'runes']);

function urlOf(url) {
  return new URL(url, 'http://page-key.invalid');
}

function segmentsOf(pathname) {
  return pathname.split('/').filter((segment) => segment !== '').map(decodeURIComponent);
}

function versionLabel(version, latest) {
  return version === null || version === latest ? LATEST : version;
}

function catalogueKey(resource, id, version) {
  return id === null ? `list:${resource}@${version}` : `detail:${resource}:${id}@${version}`;
}

function legacyDetailId(resource, name) {
  return resource === 'runes' ? (RUNE_PATH_IDS[name.toLowerCase()] ?? name) : name;
}

// The legacy grammar: `[/{version}]/{resources}` or `[/{version}]/{resource}/{name}`.
function legacyCatalogueKey(segments, version) {
  const [head, name, ...rest] = segments;
  if (rest.length > 0) return null;
  if (name === undefined && LEGACY_LISTS[head] !== undefined) {
    return catalogueKey(LEGACY_LISTS[head], null, version);
  }
  const resource = LEGACY_DETAILS[head];
  if (name === undefined || resource === undefined) return null;
  return catalogueKey(resource, legacyDetailId(resource, name), version);
}

/** Key of a legacy URL. `latest` folds an explicit latest version into the short form. */
export function legacyKey(url, latest = null) {
  const parsed = urlOf(url);
  let segments = segmentsOf(parsed.pathname);
  let version = parsed.searchParams.get('version');
  if (segments.length > 0 && VERSION.test(segments[0])) {
    version = segments[0];
    segments = segments.slice(1);
  }
  if (segments.length === 0 || (segments.length === 1 && segments[0] === 'home')) return 'home';
  const label = versionLabel(version, latest);
  return legacyCatalogueKey(segments, label) ?? `page:${segments.join('/')}`;
}

function nextDetailId(resource, segment) {
  if (!SLUGGED.has(resource)) return segment;
  const match = segment.match(/^(\d+)(?:-.*)?$/);
  return match === null ? segment : match[1];
}

function nextCatalogueKey(segments, version) {
  const [resource, segment, ...rest] = segments;
  if (!NEXT_RESOURCES.has(resource) || rest.length > 0) return null;
  const id = segment === undefined ? null : nextDetailId(resource, segment);
  return catalogueKey(resource, id, version);
}

/** Key and locale of a URL of ADR 0005's grammar, `/{locale}[/{version}]/…`. */
export function nextKey(url, latest = null) {
  const [locale = null, ...afterLocale] = segmentsOf(urlOf(url).pathname);
  let segments = afterLocale;
  let version = null;
  if (segments.length > 0 && VERSION.test(segments[0])) {
    version = segments[0];
    segments = segments.slice(1);
  }
  if (segments.length === 0) return { key: version === null ? 'home' : `page:${version}`, locale };
  const label = versionLabel(version, latest);
  return { key: nextCatalogueKey(segments, label) ?? `page:${segments.join('/')}`, locale };
}
