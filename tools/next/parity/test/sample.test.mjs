import assert from 'node:assert/strict';
import { test } from 'node:test';
import { pairs, sampleLanguages, sampleVersions } from '../lib/sample.mjs';
import { defaultLanguages, trapVersions } from '../lib/settings.mjs';

test('takes the latest versions then the traps, lolpatch builds left out', () => {
  const upstream = ['16.19.1', 'lolpatch_7.20', '16.18.1', '8.7.1', '0.151.2'];

  const versions = sampleVersions(upstream, { latest: 3 });

  assert.deepEqual(versions, ['16.19.1', '16.18.1', '8.7.1', ...trapVersions.filter(
    (version) => version !== '8.7.1')]);
});

test('an explicit list replaces the latest versions and the traps', () => {
  assert.deepEqual(sampleVersions(['16.19.1'], { explicit: ['7.22.1', '7.22.1'] }), ['7.22.1']);
});

test('languages default to the five of the sample', () => {
  assert.deepEqual(sampleLanguages(undefined), defaultLanguages);
  assert.deepEqual(sampleLanguages(['ko_KR']), ['ko_KR']);
});

test('pairs every version with every language, versions first', () => {
  assert.deepEqual(pairs({ versions: ['a', 'b'], languages: ['x', 'y'] }), [
    { version: 'a', language: 'x' }, { version: 'a', language: 'y' },
    { version: 'b', language: 'x' }, { version: 'b', language: 'y' },
  ]);
});
