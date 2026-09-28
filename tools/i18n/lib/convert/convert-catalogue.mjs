import { buildIcuPlural } from './message/build-icu-plural.mjs';
import { convertMessage } from './message/convert-message.mjs';

const SINGULAR_SUFFIX = '_one';
// The legacy front took `X_one` for a count of exactly one, whatever the locale: the CLDR
// `one` category would also cover 0 in French, and match nothing in Japanese or Chinese.
const SINGULAR_SELECTOR = '=1';

/**
 * Converts one parsed YAML catalogue, keeping its keys and nesting. The legacy front paired
 * `X` (other) with `X_one` (singular) and chose between them itself: each pair becomes the
 * ICU plural `X`, and `X_one` disappears. Returns the tree and what the conversion did.
 * `locale` is a BCP 47 tag; `excludedKeys` are top-level keys left out.
 */
export function convertCatalogue(tree, { locale, excludedKeys = [] }) {
  const stats = { messages: 0, pipePlurals: 0, pluralPairs: 0, excludedKeys: 0 };
  const kept = Object.fromEntries(
    Object.entries(tree ?? {}).filter(([key]) => !excludedKeys.includes(key)),
  );
  stats.excludedKeys = Object.keys(tree ?? {}).length - Object.keys(kept).length;
  return { tree: convertLevel(kept, { locale, stats, path: '' }), stats };
}

function convertLevel(level, context) {
  const converted = {};
  for (const [key, value] of Object.entries(level)) {
    const path = context.path ? `${context.path}.${key}` : key;
    if (isAbsorbedSingular(level, key)) {
      continue;
    }
    if (value === null) {
      throw new Error(`Empty message at ${path}`);
    }
    converted[key] =
      typeof value === 'object'
        ? convertBranch(value, { ...context, path })
        : convertLeaf(String(value), level[`${key}${SINGULAR_SUFFIX}`], context);
  }
  return converted;
}

function convertBranch(value, context) {
  if (Array.isArray(value)) {
    throw new Error(`Lists are not translatable messages: ${context.path}`);
  }
  return convertLevel(value, context);
}

function convertLeaf(message, singular, { locale, stats }) {
  stats.messages += 1;
  if (typeof singular === 'string') {
    stats.pluralPairs += 1;
    return buildIcuPlural([
      { selector: SINGULAR_SELECTOR, text: singular },
      { selector: 'other', text: message },
    ]);
  }
  const { kind, message: converted } = convertMessage(message, locale);
  stats.pipePlurals += kind === 'pipe-plural' ? 1 : 0;
  return converted;
}

function isAbsorbedSingular(level, key) {
  if (!key.endsWith(SINGULAR_SUFFIX) || typeof level[key] !== 'string') {
    return false;
  }
  return typeof level[key.slice(0, -SINGULAR_SUFFIX.length)] === 'string';
}
