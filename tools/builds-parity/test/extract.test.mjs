import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { resolve } from 'node:path';
import { after, before, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { compareReadings, normalize } from '../lib/compare.mjs';
import { modeLabels } from '../lib/modes.mjs';
import { chromiumOf, readStack } from '../lib/read.mjs';

const ROOT = resolve(fileURLToPath(import.meta.url), '../../../..');
const E2E_PACKAGE = resolve(ROOT, 'tests/LoDb.E2E/package.json');
const LABELS = modeLabels(resolve(ROOT, 'src/LoDb.Web/public/i18n'));
const TOKEN = '0123456789abcdef01234567';
const BUILDS = [
  { token: TOKEN, language: 'fr_FR' },
  { token: 'ffffffffffffffffffffffff', language: 'en_US' },
];
const installed = existsSync(resolve(ROOT, 'tests/LoDb.E2E/node_modules/@playwright/test'));

// A stack that serves one share page, `page`, at /b/TOKEN, and a 404 anywhere else.
function serve(page) {
  const html = readFileSync(new URL(`./pages/${page}`, import.meta.url), 'utf8');
  const server = createServer((request, response) => {
    const found = new URL(request.url, 'http://localhost').pathname === `/b/${TOKEN}`;
    response.writeHead(found ? 200 : 404, { 'Content-Type': 'text/html; charset=utf-8' });
    response.end(found ? html : '<!doctype html><title>404</title><h1>Not found</h1>');
  });
  return new Promise((done) => server.listen(0, '127.0.0.1', () => done(server)));
}

const base = (server) => `http://127.0.0.1:${server.address().port}`;

let browser;
let stacks;

before(async () => {
  if (!installed) return;
  browser = await chromiumOf(E2E_PACKAGE).launch();
  stacks = { legacy: await serve('legacy.html'), next: await serve('next.html') };
});

after(async () => {
  await browser?.close();
  for (const server of Object.values(stacks ?? {})) server.close();
});

async function read(stack) {
  const readings = await readStack(browser, base(stacks[stack]), BUILDS);
  return readings.map((reading) => ({ ...reading, facts: normalize(reading.facts, LABELS) }));
}

const skip = installed ? false : 'needs `npm ci --prefix tests/LoDb.E2E` (Playwright)';

test('reads what the share page of the rewrite shows', { skip }, async () => {
  const [page, missing] = await read('next');

  assert.equal(page.url, `${base(stacks.next)}/b/${TOKEN}?lang=fr_FR`);
  assert.deepEqual(page.facts, {
    name: 'Mid burst',
    champion: { name: 'Ahri', ghost: false },
    mode: 'aram',
    patch: { version: '15.14.1', current: '16.19.1' },
    vote: 3,
    runes: [
      {
        tree: 'Domination',
        keystone: { name: '9923', ghost: true },
        perks: [
          { name: 'Taste of Blood', ghost: false },
          { name: 'Eyeball Collection', ghost: false },
          { name: 'Treasure Hunter', ghost: false },
        ],
      },
      {
        tree: 'Sorcery',
        keystone: null,
        perks: [
          { name: 'Transcendence', ghost: false },
          { name: 'Scorch', ghost: false },
        ],
      },
    ],
    steps: [
      {
        label: 'Start',
        cost: 1500,
        items: [
          { name: "Doran's Ring", ghost: false },
          { name: '99', ghost: true },
        ],
      },
      { label: 'Core', cost: 3000, items: [{ name: "Rabadon's Deathcap", ghost: false }] },
    ],
    total: 4500,
  });
  assert.deepEqual([missing.status, missing.facts], [404, null]);
});

test('reads the same facts from the legacy page of the same build', { skip }, async () => {
  const legacy = await read('legacy');
  const next = await read('next');

  assert.deepEqual(
    legacy.map((reading, index) => compareReadings(reading, next[index])),
    [[], []],
  );
});
