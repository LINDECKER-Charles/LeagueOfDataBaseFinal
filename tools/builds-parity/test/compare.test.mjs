import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { compareReadings, normalize } from '../lib/compare.mjs';
import { modeLabels, modeOf } from '../lib/modes.mjs';
import { renderReport } from '../lib/report.mjs';

function catalogues(entries) {
  const directory = mkdtempSync(join(tmpdir(), 'builds-parity-'));
  for (const [locale, mode] of Object.entries(entries)) {
    writeFileSync(join(directory, `${locale}.json`), JSON.stringify({ build: { mode } }));
  }
  return directory;
}

const LABELS = modeLabels(
  catalogues({
    en: { sr: "Summoner's Rift", aram: 'ARAM', arena: 'Arena' },
    fr: { sr: "Faille de l'invocateur", aram: 'ARAM', arena: 'Arène' },
    xx: { sr: 'Clash', arena: 'Clash' },
  }),
);

function facts(overrides = {}) {
  return {
    name: 'Mid burst',
    champion: { name: 'Ahri', ghost: false },
    mode: 'ARAM',
    patch: { version: '15.14.1', current: '16.19.1' },
    vote: 3,
    runes: [{ tree: 'Domination', keystone: { name: '9923', ghost: true }, perks: [] }],
    steps: [{ label: 'Start', cost: 1500, items: [{ name: '99', ghost: true }] }],
    total: 1500,
    ...overrides,
  };
}

test('reads a mode label of any locale as its mode', () => {
  assert.equal(modeOf("Faille de l'invocateur", LABELS), 'sr');
  assert.equal(modeOf('ARAM', LABELS), 'aram');
  assert.equal(modeOf('Arène', LABELS), 'arena');
});

test('marks a label that names no mode, or several', () => {
  assert.equal(modeOf('URF', LABELS), '?URF');
  assert.equal(modeOf('Clash', LABELS), '?Clash');
});

test('finds nothing between two pages that show the same facts', () => {
  const legacy = { status: 200, facts: normalize(facts({ mode: 'ARAM' }), LABELS) };
  const next = { status: 200, facts: normalize(facts({ mode: 'ARAM' }), LABELS) };

  assert.deepEqual(compareReadings(legacy, next), []);
});

test('reads the mode in the language of each page', () => {
  const legacy = { status: 200, facts: normalize(facts({ mode: 'Arena' }), LABELS) };
  const next = { status: 200, facts: normalize(facts({ mode: 'Arène' }), LABELS) };

  assert.deepEqual(compareReadings(legacy, next), []);
});

test('names each field that differs, down to the item of a step', () => {
  const steps = [{ label: 'Start', cost: 1500, items: [{ name: '99', ghost: false }] }];
  const legacy = { status: 200, facts: facts() };
  const next = { status: 200, facts: facts({ vote: null, steps }) };

  assert.deepEqual(compareReadings(legacy, next), [
    { field: '$.vote', legacy: 3, next: null },
    { field: '$.steps[0].items[0].ghost', legacy: true, next: false },
  ]);
});

test('counts a missing step or item as a difference', () => {
  const legacy = { status: 200, facts: facts() };
  const next = { status: 200, facts: facts({ steps: [] }) };

  assert.deepEqual(compareReadings(legacy, next), [
    { field: '$.steps[0]', legacy: facts().steps[0], next: undefined },
  ]);
});

test('compares the statuses alone when a page shows no build', () => {
  assert.deepEqual(compareReadings({ status: 404, facts: null }, { status: 404, facts: null }), []);
  assert.deepEqual(compareReadings({ status: 200, facts: facts() }, { status: 404, facts: null }), [
    { field: 'status', legacy: 200, next: 404 },
  ]);
});

test('reports each page, then the differences of those that differ', () => {
  const meta = { date: '2026-09-26', legacy: 'L', next: 'N', command: 'node compare.mjs' };
  const results = [
    {
      build: { token: 'feedbeef0000000000000001', case: 'public | sr' },
      legacy: { status: 200 },
      next: { status: 200 },
      differences: [{ field: '$.vote', legacy: 2, next: null }],
    },
    {
      build: { token: 'feedbeef0000000000000002' },
      legacy: { status: 200 },
      next: { status: 200 },
      differences: [],
    },
  ];

  const report = renderReport(meta, results);

  assert.match(report, /- Pages : 2, dont 1 avec un écart/);
  assert.match(report, /\| `feedbeef0000000000000001` \| public \\\| sr \| 200 \| 200 \| 1 \|/);
  assert.match(
    report,
    /## `feedbeef0000000000000001`\n\n\| Champ .*\n.*\n\| `\$\.vote` \| 2 \| null \|/,
  );
  assert.doesNotMatch(report, /## `feedbeef0000000000000002`/);
});
