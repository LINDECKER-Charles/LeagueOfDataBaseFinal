import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { createVolume, dailyFiles, dayBefore, removeVolume } from '../lib/storage.mjs';
import { stableDump } from '../lib/schema.mjs';

const today = new Date('2026-03-01T00:30:00Z');

describe('dayBefore', () => {
  it('counts UTC days across a month boundary', () => {
    assert.equal(dayBefore(today, 0), '2026-03-01');
    assert.equal(dayBefore(today, 1), '2026-02-28');
    assert.equal(dayBefore(today, 30), '2026-01-30');
  });
});

describe('dailyFiles', () => {
  it('dates each day and writes entities, raw or entity-less payloads', () => {
    const files = dailyFiles({
      days: [
        { offset: 0, entities: { 'champion:Ahri': 2, 'item:1001': 3 } },
        { offset: 1, raw: 'not-json{' },
        { offset: 2, withoutEntities: true },
      ],
    }, today);
    assert.deepEqual(files.map((file) => file.name),
      ['2026-03-01.json', '2026-02-28.json', '2026-02-27.json']);
    assert.deepEqual(JSON.parse(files[0].content), {
      date: '2026-03-01', views: 5, entities: { 'champion:Ahri': 2, 'item:1001': 3 },
    });
    assert.equal(files[1].content, 'not-json{');
    assert.deepEqual(JSON.parse(files[2].content), { date: '2026-02-27', views: 0 });
  });
});

describe('createVolume', () => {
  it('lays out datasets and aggregates the way go-api reads them', () => {
    const volumeDir = createVolume(today);
    try {
      const storage = path.join(volumeDir, 'storage');
      assert.ok(fs.existsSync(path.join(storage, 'data/16.18.1/en_US/champion.json')));
      assert.ok(fs.existsSync(path.join(storage, 'analytics/daily/2026-03-01.json')));
    } finally {
      removeVolume(volumeDir);
    }
    assert.equal(fs.existsSync(volumeDir), false);
  });
});

describe('stableDump', () => {
  it('drops the random \\restrict wrapper of pg_dump 17', () => {
    const dump = '\\restrict AbC123\nCREATE TABLE t ();\n\\unrestrict AbC123\n';
    assert.equal(stableDump(dump), 'CREATE TABLE t ();\n');
  });
});
