// Parity of the shared builds (L5.3): the facts each /b/{token} page of tokens.json shows
// on the legacy stack and on the rewrite (champion, patch, mode, score, runes, items per
// step, ghost markers), compared field by field. Both stacks read the same copy of the
// legacy dev database, where fixture.sql wrote the builds. Read only: GET requests alone.
// Exits 1 when a page differs, 2 when a stack cannot be read.
//
//   node tools/builds-parity/compare.mjs [--legacy http://localhost:8080]
//     [--next http://localhost:18080] [--tokens tools/builds-parity/tokens.json]
//     [--out <report.md>] [--json <results.json>] [--pause-ms 0]

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { compareReadings, normalize } from './lib/compare.mjs';
import { modeLabels } from './lib/modes.mjs';
import { chromiumOf, readStack } from './lib/read.mjs';
import { renderReport } from './lib/report.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '../..');
const E2E_PACKAGE = resolve(ROOT, 'tests/LoDb.E2E/package.json');
const CATALOGUES = resolve(ROOT, 'src/LoDb.Web/public/i18n');
const EXIT_DIFFERENT = 1;
const EXIT_UNREADABLE = 2;

const { values: options } = parseArgs({
  options: {
    legacy: { type: 'string', default: 'http://localhost:8080' },
    next: { type: 'string', default: process.env.LODB_E2E_BASE_URL ?? 'http://localhost:18080' },
    tokens: { type: 'string', default: resolve(HERE, 'tokens.json') },
    out: { type: 'string' },
    json: { type: 'string' },
    'pause-ms': { type: 'string', default: '0' },
  },
});

function commandLine() {
  const args = process.argv.slice(2).map((arg) => (/\s/.test(arg) ? `"${arg}"` : arg));
  return ['node tools/builds-parity/compare.mjs', ...args].join(' ');
}

function write(path, content) {
  mkdirSync(dirname(resolve(path)), { recursive: true });
  writeFileSync(path, content);
}

async function run() {
  const builds = JSON.parse(readFileSync(options.tokens, 'utf8'));
  const labels = modeLabels(CATALOGUES);
  const browser = await chromiumOf(E2E_PACKAGE).launch();
  try {
    const pause = Number(options['pause-ms']);
    const legacy = await readStack(browser, options.legacy, builds, pause);
    const next = await readStack(browser, options.next, builds, pause);
    return builds.map((build, index) => {
      const pair = [legacy[index], next[index]].map((reading) => ({
        ...reading,
        facts: normalize(reading.facts, labels),
      }));
      return { build, legacy: pair[0], next: pair[1], differences: compareReadings(...pair) };
    });
  } finally {
    await browser.close();
  }
}

try {
  const results = await run();
  const meta = {
    date: new Date().toISOString().slice(0, 10),
    legacy: options.legacy,
    next: options.next,
    command: commandLine(),
  };
  if (options.out !== undefined) write(options.out, renderReport(meta, results));
  if (options.json !== undefined) write(options.json, JSON.stringify(results, null, 1));
  const differing = results.filter((result) => result.differences.length > 0);
  for (const { build, differences } of differing) {
    for (const { field, legacy, next } of differences) {
      console.log(`${build.token} ${field}: ${JSON.stringify(legacy)} → ${JSON.stringify(next)}`);
    }
  }
  console.log(`builds-parity: ${results.length} pages, ${differing.length} with a difference.`);
  process.exitCode = differing.length > 0 ? EXIT_DIFFERENT : 0;
} catch (error) {
  console.error(`builds-parity: ${error.message}`);
  process.exitCode = EXIT_UNREADABLE;
}
