import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { reduceSkins } from '../lib/reduce/reduce-cdragon.mjs';
import {
  championKeys,
  reduceChampions,
  reduceVersions,
} from '../lib/reduce/reduce-ddragon.mjs';

describe('reduceVersions', () => {
  it('keeps the recorded versions and the first legacy entries, in upstream order', () => {
    const versions = [
      '16.2.1',
      '16.1.1',
      '0.151.2',
      'lolpatch_7.20',
      'lolpatch_7.19',
      'lolpatch_3.7',
    ];

    const { document } = reduceVersions(versions, ['0.151.2', '16.2.1'], 2);

    assert.deepEqual(document, ['16.2.1', '0.151.2', 'lolpatch_7.20', 'lolpatch_7.19']);
  });
});

describe('reduceChampions', () => {
  const file = {
    type: 'champion',
    keys: { 103: 'Ahri', 9: 'FiddleSticks', 86: 'Garen' },
    data: {
      Ahri: { id: 'Ahri', key: '103', recommended: [{ blocks: [] }] },
      FiddleSticks: { id: 'FiddleSticks', key: '9' },
      Garen: { id: 'Garen', key: '86' },
    },
  };

  it('matches ids without case, drops the rest and the item sets', () => {
    const { document } = reduceChampions(structuredClone(file), ['Ahri', 'Fiddlesticks']);

    assert.deepEqual(Object.keys(document.data), ['Ahri', 'FiddleSticks']);
    assert.equal(document.data.Ahri.recommended, undefined);
    assert.deepEqual(document.keys, { 103: 'Ahri', 9: 'FiddleSticks' });
    assert.deepEqual(championKeys(document), ['103', '9']);
  });
});

describe('reduceSkins', () => {
  it("keeps the recorded champions' skins with their chroma fields only", () => {
    const skins = {
      103001: {
        id: 103001,
        name: 'Dynasty Ahri',
        splashPath: '/big.jpg',
        chromas: [{ id: 103052, name: 'x', chromaPath: '/c.png', colors: ['#fff'], extra: 1 }],
      },
      86000: { id: 86000, name: 'Garen', splashPath: '/big.jpg' },
    };

    const { document } = reduceSkins(skins, new Set(['103']));

    assert.deepEqual(document, {
      103001: {
        id: 103001,
        name: 'Dynasty Ahri',
        chromas: [{ id: 103052, name: 'x', chromaPath: '/c.png', colors: ['#fff'] }],
      },
    });
  });
});
