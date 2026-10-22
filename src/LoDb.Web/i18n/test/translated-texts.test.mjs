// The texts no locale may leave to the `en` fallback: the patch history, the links that lead
// to it from every page of the catalogue (the footer, the history of a detail), and what a
// detail and its head say of its entity's states. The other scopes are not all translated
// yet (`npm run i18n:report`), hence a list of keys.
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { describe, test } from 'node:test';
import { readSourceCatalogues } from '../lib/read-source-catalogues.mjs';

const SOURCES = join(import.meta.dirname, '..', '..', 'public', 'i18n');
const REFERENCE = 'en';
const PATCHES = 'patches';
const SEO = 'seo';
const ROOT = '';
const ROOT_KEYS = ['footer.navigation.patches', 'detail.history.title'];
// Every key of en under these, in every locale: the blocks of a detail and their field labels.
const ROOT_GROUPS = ['detail.history', 'detail.changes', 'detail.field'];
const SEO_KEYS = [
  'state_range_suffix',
  'state.summary',
  'state.summary_more',
  'state.summary_new',
  'state.summary_returned',
];
const ANY_TEXT = /\S/;
const PARAMETER = /{{\s*\w+\s*}}/g;

const { locales, scopes } = readSourceCatalogues(SOURCES);

// A text of the locale, not empty, naming the parameters the en text names.
function expectSameText(text, reference, label) {
  assert.match(String(text ?? ''), ANY_TEXT, label);
  assert.deepEqual(parametersOf(text), parametersOf(reference), label);
}

// Every key of a group of en, translated with the same parameters.
function expectSameTexts(group, reference, label) {
  for (const [key, text] of Object.entries(reference)) {
    expectSameText(group?.[key], text, `${label}: ${key}`);
  }
}

function parametersOf(text) {
  return [...String(text ?? '').matchAll(PARAMETER)].map(([parameter]) => parameter).sort();
}

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

  test('the blocks of the states and their field labels, with the same parameters', () => {
    for (const group of ROOT_GROUPS) {
      const reference = valueAt(scopes.get(ROOT).get(REFERENCE), group);
      for (const locale of locales) {
        expectSameTexts(
          valueAt(scopes.get(ROOT).get(locale), group),
          reference,
          `${locale} ${group}`,
        );
      }
    }
  });

  test("the head of a state's opener, with the same parameters", () => {
    const reference = scopes.get(SEO).get(REFERENCE);
    for (const locale of locales) {
      const catalogue = scopes.get(SEO).get(locale);
      for (const key of SEO_KEYS) {
        expectSameText(valueAt(catalogue, key), valueAt(reference, key), `${locale}: ${key}`);
      }
    }
  });
});
