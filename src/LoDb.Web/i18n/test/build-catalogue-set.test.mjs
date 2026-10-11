import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { buildCatalogueSet } from '../lib/build-catalogue-set.mjs';

const CHROME_KEYS = { about: ['index.title'] };

function sources(scopes) {
  return {
    locales: ['en', 'fr', 'ja'],
    scopes: new Map(
      Object.entries(scopes).map(([scope, byLocale]) => [scope, new Map(Object.entries(byLocale))]),
    ),
  };
}

const VALID = {
  '': {
    en: { filter: { results: '{count, plural, =1 {# result} other {# results}}' }, nav: 'Home' },
    fr: { nav: 'Accueil' },
  },
  about: {
    en: { index: { title: 'About' }, faq: 'FAQ' },
    fr: { index: { title: 'À propos' }, faq: 'FAQ' },
  },
};

describe('buildCatalogueSet', () => {
  test('ships every scope in every locale, completed with en', () => {
    const { files, errors } = buildCatalogueSet(sources(VALID), CHROME_KEYS);

    assert.deepEqual(errors, []);
    assert.deepEqual([...files.keys()].sort(), [
      'about/en.json',
      'about/fr.json',
      'about/ja.json',
      'en.json',
      'fr.json',
      'ja.json',
    ]);
    assert.equal(files.get('fr.json').nav, 'Accueil');
    assert.equal(files.get('ja.json').filter.results, VALID[''].en.filter.results);
    assert.deepEqual(files.get('about/ja.json'), { index: { title: 'About' }, faq: 'FAQ' });
  });

  test('lends the chrome its labels of other scopes through the root catalogue', () => {
    const { files } = buildCatalogueSet(sources(VALID), CHROME_KEYS);

    assert.deepEqual(files.get('fr.json').about, { index: { title: 'À propos' } });
    assert.deepEqual(files.get('ja.json').about, { index: { title: 'About' } });
  });

  test('lists every scope without en, and every label it cannot lend', () => {
    const broken = {
      '': { en: { b: 'ok' } },
      about: { fr: { index: { title: 'À propos' } } },
    };

    const { errors } = buildCatalogueSet(sources(broken), CHROME_KEYS);

    assert.ok(errors.includes('about: no en catalogue'));
    assert.ok(errors.some((error) => error.startsWith('about/en.json: no message at index.title')));
  });

  test('refuses a borrowed scope the root catalogue already names', () => {
    const clash = { ...VALID, '': { en: { about: 'About us' } } };

    const { errors } = buildCatalogueSet(sources(clash), CHROME_KEYS);

    assert.ok(errors.includes('en.json: the root catalogue already has a "about" key'));
  });
});
