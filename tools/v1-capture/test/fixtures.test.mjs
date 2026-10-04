// Consistency of the committed fixtures: keys, data set, scenarios and references must
// describe the same timeline, or a replay (here or in L6.3) would compare the wrong thing.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { expandSteps, loadGroups } from '../lib/scenarios.mjs';
import { referencesDir, scenariosDir, seedDir } from '../lib/settings.mjs';

const keys = JSON.parse(fs.readFileSync(path.join(seedDir, 'keys.json'), 'utf8')).keys;
const dataset = fs.readFileSync(path.join(seedDir, 'dataset.sql'), 'utf8');
const groups = loadGroups(scenariosDir);
const rawKeyShape = /^lodb_[0-9a-f]{40}$/;
// Never stored: the unknown key must stay unknown, late_key is inserted by a step.
const unseeded = new Set(['unknown', 'late_key']);

const readReference = (name) => JSON.parse(
  fs.readFileSync(path.join(referencesDir, `${name}.json`), 'utf8'),
);

describe('keys.json and dataset.sql', () => {
  it('holds well-formed raw keys', () => {
    for (const [alias, raw] of Object.entries(keys)) {
      assert.match(raw, rawKeyShape, alias);
    }
  });

  it('seeds every key except the unseeded ones, under its alias', () => {
    for (const [alias, raw] of Object.entries(keys)) {
      assert.equal(dataset.includes(`'${alias}', '${raw}'`), !unseeded.has(alias), alias);
    }
  });
});

describe('scenarios', () => {
  it('only reference known key aliases', () => {
    const used = JSON.stringify(groups).matchAll(/\{\{key:([a-z_]+)\}\}/g);
    for (const [, alias] of used) {
      assert.ok(alias in keys, alias);
    }
  });

  it('never write the unknown key into the database', () => {
    const sql = groups.flatMap((group) => group.steps).map((step) => step.sql ?? '').join();
    assert.equal(sql.includes(keys.unknown), false);
  });
});

describe('references', () => {
  for (const group of groups) {
    it(`${group.name} follows its scenario step by step`, () => {
      const reference = readReference(group.name);
      const recorded = reference.exchanges.map(({ response, ...step }) => step);
      const expected = expandSteps(group.steps).map(({ volatile, ...step }) => step);
      assert.deepEqual(recorded, expected);
      assert.ok(reference.exchanges.every((step) => step.action || step.response));
    });
  }
});
