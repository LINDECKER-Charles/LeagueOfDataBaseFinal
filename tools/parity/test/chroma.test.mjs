import assert from 'node:assert/strict';
import { test } from 'node:test';
import { labelChromas, legacyChromaLabel } from '../lib/chroma.mjs';

test('replaces the colours of every chroma with its label', () => {
  const projection = {
    champions: {
      entries: [
        {
          id: 'Ahri',
          skins: [{ id: '103001', chromas: [{ id: 7, name: 'n', colors: ['#f00'] }] }],
        },
        { id: 'Annie', detail: false },
      ],
    },
  };

  labelChromas(projection, ({ name, colors }) => `${name}:${colors.join('/')}`);

  assert.deepEqual(projection.champions.entries[0].skins[0].chromas, [
    { id: 7, name: 'n', label: 'n:#f00' },
  ]);
  assert.equal(projection.champions.entries[1].skins, undefined);
});

test('loads the rule the legacy front ships', async () => {
  const label = await legacyChromaLabel();

  assert.equal(label({ name: 'Star Guardian Ahri (Ruby)', colors: [] }), 'Ruby');
  assert.equal(label({ name: 'Ahri', colors: ['#DF9117'] }), 'Amber');
  assert.equal(label({ name: 'Ahri', colors: [] }), 'Chroma');
});
