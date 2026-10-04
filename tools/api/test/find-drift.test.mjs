import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { parsePorcelain } from '../lib/find-drift.mjs';

describe('parsePorcelain', () => {
  it('reads modified, deleted and new files', () => {
    const output = [
      ' M src/LoDb.Api/openapi/LoDb.Api_app.json',
      ' D src/LoDb.Web/src/app/core/api/generated/models/edition.ts',
      '?? src/LoDb.Web/src/app/core/api/generated/models/tier.ts',
      '',
    ].join('\n');

    assert.deepEqual(parsePorcelain(output), [
      { status: 'M', path: 'src/LoDb.Api/openapi/LoDb.Api_app.json' },
      { status: 'D', path: 'src/LoDb.Web/src/app/core/api/generated/models/edition.ts' },
      { status: '??', path: 'src/LoDb.Web/src/app/core/api/generated/models/tier.ts' },
    ]);
  });

  it('finds no drift in an empty status', () => {
    assert.deepEqual(parsePorcelain(''), []);
  });
});
