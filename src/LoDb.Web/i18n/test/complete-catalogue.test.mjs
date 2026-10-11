import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { completeCatalogue } from '../lib/complete-catalogue.mjs';

describe('completeCatalogue', () => {
  const reference = { title: 'Items', filter: { page: 'Page', next: 'Next' }, empty: 'None' };

  test('fills each key the locale lacks with the reference message, in its order', () => {
    const complete = completeCatalogue(reference, { filter: { next: 'Suivant' }, title: 'Objets' });

    assert.deepEqual(complete, {
      title: 'Objets',
      filter: { page: 'Page', next: 'Suivant' },
      empty: 'None',
    });
    assert.deepEqual(Object.keys(complete), ['title', 'filter', 'empty']);
  });

  test('treats an empty message as missing, as Transloco did', () => {
    assert.equal(completeCatalogue(reference, { empty: '' }).empty, 'None');
  });

  test('keeps the keys only the locale has, and the reference whole for a missing scope', () => {
    assert.equal(completeCatalogue(reference, { extra: 'Seulement ici' }).extra, 'Seulement ici');
    assert.deepEqual(completeCatalogue(reference, {}), reference);
  });

  test('keeps a reference group where the locale has a message instead', () => {
    assert.deepEqual(completeCatalogue(reference, { filter: 'Filtre' }).filter, reference.filter);
  });
});
