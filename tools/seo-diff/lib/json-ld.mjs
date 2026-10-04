// JSON-LD of both sites, made comparable. Every URL of the site becomes the key of the page
// it names (page-key.mjs), or `asset:` and its path for a file; blocks are then flattened to
// `path → value` and compared field by field.

const FILE = /\.[a-z0-9]+$/i;

/**
 * A function that rewrites the URLs of one site: those under one of `origins` become
 * `keyOf(path)` (plus their fragment), or `asset:{path}` for a file; any other string is kept.
 */
export function urlRewriter(origins, keyOf) {
  const isOfSite = (text) =>
    origins.some((origin) => text === origin || text.startsWith(`${origin}/`));
  return (text) => {
    if (!isOfSite(text)) return text;
    const url = new URL(text);
    if (FILE.test(url.pathname)) return `asset:${url.pathname}`;
    return `${keyOf(url.pathname + url.search)}${url.hash}`;
  };
}

/** A deep copy of `value` whose strings went through `rewrite`. */
export function normalizeJsonLd(value, rewrite) {
  if (typeof value === 'string') return rewrite(value);
  if (Array.isArray(value)) return value.map((entry) => normalizeJsonLd(entry, rewrite));
  if (value === null || typeof value !== 'object') return value;
  return Object.fromEntries(
    Object.entries(value).map(([key, entry]) => [key, normalizeJsonLd(entry, rewrite)]),
  );
}

function typeName(type) {
  return Array.isArray(type) ? type.join(',') : String(type);
}

function collectTypes(value, types) {
  if (Array.isArray(value)) {
    value.forEach((entry) => collectTypes(entry, types));
  } else if (value !== null && typeof value === 'object') {
    if (value['@type'] !== undefined) types.add(typeName(value['@type']));
    Object.values(value).forEach((entry) => collectTypes(entry, types));
  }
  return types;
}

/** Every `@type` the blocks use, nested ones included, sorted and without repeats. */
export function typesOf(blocks) {
  return [...collectTypes(blocks, new Set())].sort();
}

/** Name of a block: `@graph` for the site graph, its `@type` otherwise. */
export function blockName(block) {
  if (block !== null && typeof block === 'object' && Array.isArray(block['@graph'])) {
    return '@graph';
  }
  return typeName(block?.['@type'] ?? '?');
}

function flattenInto(value, path, out) {
  if (Array.isArray(value)) {
    value.forEach((entry, index) => flattenInto(entry, `${path}[${index}]`, out));
  } else if (value !== null && typeof value === 'object') {
    for (const [key, entry] of Object.entries(value)) flattenInto(entry, `${path}.${key}`, out);
  } else {
    out.set(path, value);
  }
  return out;
}

/** `path → value` of every leaf of the blocks, each path rooted at its block's name. */
export function flattenBlocks(blocks) {
  const out = new Map();
  for (const block of blocks) flattenInto(block, blockName(block), out);
  return out;
}

/** The leaves whose values differ, or exist on one side only, in path order. */
export function diffBlocks(prodBlocks, nextBlocks) {
  const prod = flattenBlocks(prodBlocks);
  const next = flattenBlocks(nextBlocks);
  const paths = [...new Set([...prod.keys(), ...next.keys()])].sort();
  return paths
    .filter((path) => prod.get(path) !== next.get(path))
    .map((path) => ({ path, prod: prod.get(path), next: next.get(path) }));
}
