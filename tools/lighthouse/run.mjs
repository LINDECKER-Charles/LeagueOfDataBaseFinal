// Lighthouse budgets of the lot 3 milestone (L3.13): five pages of the stack, mobile profile,
// median of three runs, against the budgets of lib/budgets.mjs; production is measured with
// the same settings, for information only. Writes docs/reecriture/rapports/lighthouse.md and
// exits 1 when a page of the stack misses a budget, 2 when a page cannot be measured.
//
//   node tools/lighthouse/run.mjs [--next http://localhost:18080] [--stack "lodb-dev"]
//     [--prod https://league-of-data-base.com | --skip-prod] [--runs 3] [--out <report.md>]
//     [--json <results.json>]

import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { checkBudgets } from './lib/budgets.mjs';
import { measureSite } from './lib/measure.mjs';
import { PAGES } from './lib/pages.mjs';
import { renderReport } from './lib/report.mjs';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const E2E_PACKAGE = resolve(ROOT, 'tests/LoDb.E2E/package.json');
const DEFAULT_REPORT = 'docs/reecriture/rapports/lighthouse.md';
const EXIT_OVER_BUDGET = 1;
const EXIT_UNMEASURABLE = 2;

const { values: options } = parseArgs({
  options: {
    next: { type: 'string', default: process.env.LODB_E2E_BASE_URL ?? 'http://localhost:18080' },
    stack: { type: 'string', default: 'lodb-dev' },
    prod: { type: 'string', default: 'https://league-of-data-base.com' },
    'skip-prod': { type: 'boolean', default: false },
    runs: { type: 'string', default: '3' },
    out: { type: 'string', default: DEFAULT_REPORT },
    json: { type: 'string' },
  },
});

function commandLine() {
  const args = process.argv.slice(2).map((arg) => (/\s/.test(arg) ? `"${arg}"` : arg));
  return ['node tools/lighthouse/run.mjs', ...args].join(' ');
}

async function run() {
  const settings = { e2ePackage: E2E_PACKAGE, runs: Number(options.runs) };
  const pagesOf = (key) => PAGES.map((page) => ({ name: page.name, path: page[key] }));
  const next = await measureSite(options.next, pagesOf('next'), settings);
  const prod = options['skip-prod']
    ? []
    : await measureSite(options.prod, pagesOf('prod'), settings);
  const meta = {
    date: new Date().toISOString().slice(0, 10),
    next: options.next,
    prod: options.prod,
    stack: options.stack,
    runs: settings.runs,
    command: commandLine(),
  };
  const out = resolve(ROOT, options.out);
  mkdirSync(dirname(out), { recursive: true });
  writeFileSync(out, renderReport(meta, next, prod));
  if (options.json !== undefined) {
    writeFileSync(options.json, JSON.stringify({ next, prod }, null, 1));
  }
  return { out, next };
}

try {
  const { out, next } = await run();
  const missed = next.filter((page) => checkBudgets(page).some((check) => !check.met));
  console.log(
    `lighthouse: ${next.length} pages, ${missed.length} over budget; ` +
      `report written to ${relative(ROOT, out)}.`,
  );
  process.exitCode = missed.length > 0 ? EXIT_OVER_BUDGET : 0;
} catch (error) {
  console.error(`lighthouse: ${error.message}`);
  process.exitCode = EXIT_UNMEASURABLE;
}
