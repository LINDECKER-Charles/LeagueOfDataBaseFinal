import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { differences } from '../lib/compare.mjs';

describe('differences', () => {
  it('ignores key order', () => {
    assert.deepEqual(differences({ a: 1, b: [1, 2] }, { b: [1, 2], a: 1 }), []);
  });

  it('reports changed, missing and extra values with their path', () => {
    const found = differences(
      { status: 200, headers: { allow: 'GET' } },
      { status: 404, headers: {}, text: 'x' },
    );
    assert.deepEqual(found, [
      '/status: expected 200, got 404',
      '/headers/allow: expected "GET", got undefined',
      '/text: expected undefined, got "x"',
    ]);
  });

  it('labels exchanges by step id', () => {
    const expected = { exchanges: [{ id: 'burst#2', response: { status: 200 } }] };
    const actual = { exchanges: [{ id: 'burst#2', response: { status: 429 } }] };
    assert.deepEqual(differences(expected, actual),
      ['/exchanges/0(burst#2)/response/status: expected 200, got 429']);
  });

  it('tells an array from an object', () => {
    assert.deepEqual(differences([], {}), ['/: expected an array']);
  });
});
