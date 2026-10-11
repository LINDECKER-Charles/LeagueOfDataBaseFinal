// The shipped catalogues render exactly what MessageFormat rendered from their sources, the
// runtime the site had before its ICU messages were compiled at build time: every message of
// every scope in all 21 locales, an ICU one for a spread of argument values.
import MessageFormat from '@messageformat/core';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { before, describe, test } from 'node:test';
import { buildCatalogueSet } from '../lib/build-catalogue-set.mjs';
import { readSourceCatalogues } from '../lib/read-source-catalogues.mjs';
import { importTypeScript } from './support/import-typescript.mjs';

const WORKSPACE = join(import.meta.dirname, '..', '..');
const MESSAGES = join(WORKSPACE, 'src', 'app', 'core', 'i18n', 'messages');
const REFERENCE = 'en';
// Counts across CLDR's categories (zero, one, two, few, many), with fractions and big numbers.
const COUNTS = [
  0, 1, 2, 3, 4, 5, 7, 11, 12, 15, 21, 22, 25, 100, 101, 102, 1000, 1234567, 0.5, 1.5,
];
const UNLISTED = 'unlisted';
const TRANSLOCO_PARAMETER = /{{[^{}]*?}}/;

let sources;
let built;
let formatCompiledMessage;
let LocaleFormats;

function flatten(tree, prefix = '', flat = new Map()) {
  for (const [key, value] of Object.entries(tree ?? {})) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
      flatten(value, path, flat);
    } else {
      flat.set(path, value);
    }
  }
  return flat;
}

// What each argument of a compiled message is: counted (plural, #), selected, or shown.
function argumentsOf(parts, found = { counted: new Set(), selected: new Map(), shown: new Set() }) {
  for (const part of parts) {
    if (!Array.isArray(part)) continue;
    const [kind, name] = part;
    if (kind === 'arg') found.shown.add(name);
    if (kind === '#' || kind === 'plural' || kind === 'selectordinal') found.counted.add(name);
    if (kind === 'select') found.selected.set(name, [...Object.keys(part[2]), UNLISTED]);
    const cases = kind === 'select' ? part[2] : kind === '#' || kind === 'arg' ? {} : part[3];
    for (const tokens of Object.values(cases)) argumentsOf(tokens, found);
  }
  return found;
}

function* parameterSets(parts) {
  const { counted, selected, shown } = argumentsOf(parts);
  const choices = [...selected].reduce(
    (sets, [name, keys]) => sets.flatMap((set) => keys.map((key) => ({ ...set, [name]: key }))),
    [{}],
  );
  for (const count of COUNTS) {
    for (const choice of choices) {
      const params = { ...choice };
      for (const name of shown) params[name] = `«${name}»`;
      for (const name of counted) params[name] = count;
      yield params;
    }
  }
}

const formatters = new Map();
function messageFormat(locale) {
  if (!formatters.has(locale)) {
    formatters.set(locale, new MessageFormat(locale, { strictPluralKeys: false }));
  }
  return formatters.get(locale);
}

function sourceMessage(scope, locale, key) {
  const own = flatten(sources.scopes.get(scope).get(locale)).get(key);
  return typeof own === 'string' && own !== ''
    ? own
    : flatten(sources.scopes.get(scope).get(REFERENCE)).get(key);
}

before(async () => {
  sources = readSourceCatalogues(join(WORKSPACE, 'public', 'i18n'));
  built = buildCatalogueSet(sources, {}).files;
  ({ formatCompiledMessage } = await importTypeScript(
    join(MESSAGES, 'format-compiled-message.ts'),
  ));
  ({ LocaleFormats } = await importTypeScript(join(MESSAGES, 'locale-formats.ts')));
});

describe('the shipped catalogues against MessageFormat', () => {
  test('cover the 21 locales of the site', () => {
    assert.equal(sources.locales.length, 21);
  });

  test('render every ICU message of every locale as MessageFormat did', (context) => {
    let compared = 0;
    for (const [scope, catalogues] of sources.scopes) {
      for (const locale of sources.locales) {
        const formats = new LocaleFormats(locale);
        const file = scope === '' ? `${locale}.json` : `${scope}/${locale}.json`;
        for (const [key, value] of flatten(built.get(file))) {
          if (!Array.isArray(value)) continue;
          const reference = messageFormat(locale).compile(sourceMessage(scope, locale, key));
          for (const params of parameterSets(value)) {
            const expected = String(reference(params));
            assert.equal(
              formatCompiledMessage(value, params, formats),
              expected,
              `${file} ${key} ${JSON.stringify(params)}`,
            );
            compared += 1;
          }
        }
        assert.ok(catalogues.size > 0);
      }
    }
    assert.ok(compared > 1000, `only ${compared} renderings compared`);
    context.diagnostic(`${compared} renderings compared`);
  });

  test('ship every other message as the text MessageFormat rendered from it', () => {
    for (const [scope] of sources.scopes) {
      for (const locale of sources.locales) {
        const file = scope === '' ? `${locale}.json` : `${scope}/${locale}.json`;
        for (const [key, value] of flatten(built.get(file))) {
          const source = sourceMessage(scope, locale, key);
          if (Array.isArray(value) || TRANSLOCO_PARAMETER.test(source)) continue;
          assert.equal(value, messageFormat(locale).compile(source)(), `${file} ${key}`);
        }
      }
    }
  });

  // The legacy lists took their singular for a count of exactly one, whatever the locale.
  test('count the list results with the singular for one exactly', () => {
    const counts = (locale, key) =>
      [0, 1, 2].map((count) =>
        formatCompiledMessage(
          flatten(built.get(`${locale}.json`)).get(`filter.${key}`),
          { count },
          new LocaleFormats(locale),
        ),
      );

    assert.deepEqual(counts('fr', 'results'), ['0 résultats', '1 résultat', '2 résultats']);
    assert.deepEqual(counts('ja', 'results'), ['0 results', '1 result', '2 results']);
    assert.deepEqual(counts('fr', 'show_results'), [
      'Voir 0 résultats',
      'Voir 1 résultat',
      'Voir 2 résultats',
    ]);
    assert.deepEqual(counts('ja', 'show_results'), [
      'Show 0 results',
      'Show 1 result',
      'Show 2 results',
    ]);
  });
});
