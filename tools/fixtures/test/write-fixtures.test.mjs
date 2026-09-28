import assert from 'node:assert/strict';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, before, describe, it } from 'node:test';
import {
  bodyPath,
  INDEX_FILE,
  layOut,
  SIZE_BUDGET_BYTES,
  writeFixtures,
} from '../lib/write-fixtures.mjs';

const ROLES = { latest: '16.2.1', previous: '16.1.1' };
const entry = (url, body) => ({ url, status: 200, contentType: 'application/json', body });

describe('layOut', () => {
  it('sorts the index by URL and places bodies under host and path', () => {
    const entries = new Map([
      ['https://b.test/z.json', entry('https://b.test/z.json', Buffer.from('1'))],
      ['https://a.test/y.json', { url: 'https://a.test/y.json', status: 403 }],
    ]);

    const { files } = layOut(entries, { recordedOn: '2026-01-01', roles: ROLES });

    const index = JSON.parse(files.get(INDEX_FILE).toString('utf8'));
    assert.deepEqual(
      index.responses.map((response) => response.url),
      ['https://a.test/y.json', 'https://b.test/z.json'],
    );
    assert.deepEqual([...files.keys()], ['b.test/z.json', INDEX_FILE]);
    assert.equal(bodyPath('https://b.test/z.json'), 'b.test/z.json');
  });

  it('refuses bodies that differ only by case', () => {
    const upper = 'https://a.test/img/FiddleSticks_0.jpg';
    const lower = 'https://a.test/img/Fiddlesticks_0.jpg';
    const entries = new Map([
      [upper, entry(upper, Buffer.from('1'))],
      [lower, entry(lower, Buffer.from('2'))],
    ]);

    assert.throws(() => layOut(entries, { recordedOn: '2026-01-01', roles: ROLES }), /case/);
  });
});

describe('writeFixtures', () => {
  let root;
  let target;

  before(() => {
    root = mkdtempSync(join(tmpdir(), 'lodb-fixtures-'));
    target = join(root, 'ddragon');
    mkdirSync(target);
    writeFileSync(join(target, INDEX_FILE), 'previous');
  });

  after(() => rmSync(root, { recursive: true, force: true }));

  it('leaves the previous recording in place when over the budget', () => {
    const files = new Map([[INDEX_FILE, Buffer.from('next')]]);

    assert.throws(() => writeFixtures(target, { files, size: SIZE_BUDGET_BYTES + 1 }), /budget/);

    assert.equal(readFileSync(join(target, INDEX_FILE), 'utf8'), 'previous');
  });

  it('replaces the whole recording', () => {
    writeFileSync(join(target, 'stale.json'), 'stale');
    const files = new Map([
      [INDEX_FILE, Buffer.from('next')],
      ['a.test/y.json', Buffer.from('1')],
    ]);

    writeFixtures(target, { files, size: 5 });

    assert.equal(readFileSync(join(target, INDEX_FILE), 'utf8'), 'next');
    assert.equal(readFileSync(join(target, 'a.test', 'y.json'), 'utf8'), '1');
    assert.equal(existsSync(join(target, 'stale.json')), false);
    assert.equal(existsSync(`${target}.staging`), false);
  });
});
