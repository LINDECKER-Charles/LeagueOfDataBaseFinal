// The texts no locale may leave to the `en` fallback: the patch history, and the links that
// lead to it from every page of the catalogue (the footer, the previous patches of a detail).
// The other scopes are not all translated yet (`npm run i18n:report`), hence a list of keys.
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { describe, test } from 'node:test';
import { readSourceCatalogues } from '../lib/read-source-catalogues.mjs';

const SOURCES = join(import.meta.dirname, '..', '..', 'public', 'i18n');
const REFERENCE = 'en';
const PATCHES = 'patches';
const ROOT = '';
const ROOT_KEYS = ['footer.navigation.patches', 'detail.history.title'];
const ANY_TEXT = /\S/;

const { locales, scopes } = readSourceCatalogues(SOURCES);

// A dotted key into a catalogue: `footer.navigation.patches`.
function valueAt(catalogue, key) {
  return key
    .split('.')
    .reduce(
      (node, part) => (node !== null && typeof node === 'object' ? node[part] : undefined),
      catalogue,
    );
}

describe('the texts translated in every locale', () => {
  test('the patch history: every key of en, in its order, none left empty', () => {
    const catalogues = scopes.get(PATCHES);
    const keys = Object.keys(catalogues.get(REFERENCE));
    for (const locale of locales) {
      const catalogue = catalogues.get(locale) ?? {};
      assert.deepEqual(Object.keys(catalogue), keys, locale);
      for (const key of keys) {
        assert.match(String(catalogue[key]), ANY_TEXT, `${locale}: ${key}`);
      }
      assert.ok(catalogue.group.includes('{{ major }}'), `${locale}: group`);
    }
  });

  test('the root texts that lead to it', () => {
    for (const locale of locales) {
      for (const key of ROOT_KEYS) {
        const text = valueAt(scopes.get(ROOT).get(locale), key);
        assert.match(String(text ?? ''), ANY_TEXT, `${locale}: ${key}`);
      }
    }
  });
});
