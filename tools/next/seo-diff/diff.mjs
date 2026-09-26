// Diff SEO of the rewrite against production (L3.13): title, description, canonical and
// JSON-LD of a sample of production pages, compared with the pages the stack's legacy
// redirects name. Production is read only. Writes docs/reecriture/rapports/diff-seo.md and
// exits 1 when a difference is left unexplained (rules.mjs), 2 when a site cannot be read.
//
//   node tools/next/seo-diff/diff.mjs [--next http://localhost:18080]
//     [--prod https://league-of-data-base.com] [--stack "lodb-next"] [--out <report.md>]
//     [--json <results.json>] [--pause-ms 250]

import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { classifyPair } from './lib/classify.mjs';
import { collect } from './lib/collect.mjs';
import { renderReport } from './lib/report.mjs';
import { SAMPLE } from './lib/sample.mjs';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const DEFAULT_REPORT = 'docs/reecriture/rapports/diff-seo.md';
const EXIT_UNEXPLAINED = 1;
const EXIT_UNREADABLE = 2;

const { values: options } = parseArgs({
  options: {
    next: { type: 'string', default: process.env.LODB_E2E_BASE_URL ?? 'http://localhost:18080' },
    prod: { type: 'string', default: 'https://league-of-data-base.com' },
    stack: { type: 'string', default: 'lodb-next' },
    out: { type: 'string', default: DEFAULT_REPORT },
    json: { type: 'string' },
    'pause-ms': { type: 'string', default: '250' },
  },
});

async function latestOf(next) {
  const response = await fetch(new URL('/api/meta', next));
  const meta = await response.json();
  if (typeof meta.latest !== 'string') throw new Error('the stack has not ingested any version');
  return meta.latest;
}

// Rendered pages carry the stack's host without its port (nginx forwards `Host: $host`);
// prerendered ones carry the canonical origin of production.
function nextOrigins(next, prod) {
  const url = new URL(next);
  return [url.origin, `${url.protocol}//${url.hostname}`, prod];
}

function commandLine() {
  const args = process.argv.slice(2).map((arg) => (/\s/.test(arg) ? `"${arg}"` : arg));
  return ['node tools/next/seo-diff/diff.mjs', ...args].join(' ');
}

async function run() {
  const latest = await latestOf(options.next);
  const origins = { prod: options.prod, next: nextOrigins(options.next, options.prod) };
  const pairs = await collect(SAMPLE, options, Number(options['pause-ms']));
  const results = pairs.map((pair) => ({
    ...pair,
    findings: classifyPair({ ...pair, latest, origins }),
  }));
  const meta = {
    date: new Date().toISOString().slice(0, 10),
    prod: options.prod,
    next: options.next,
    stack: options.stack,
    latest,
    command: commandLine(),
  };
  const out = resolve(ROOT, options.out);
  mkdirSync(dirname(out), { recursive: true });
  writeFileSync(out, renderReport(meta, results));
  if (options.json !== undefined) writeFileSync(options.json, JSON.stringify(results, null, 1));
  return { out, results };
}

try {
  const { out, results } = await run();
  const findings = results.flatMap((result) => result.findings);
  const unexplained = findings.filter((finding) => finding.outcome === 'unexplained').length;
  const defects = findings.filter((finding) => finding.outcome === 'defect').length;
  console.log(
    `seo-diff: ${results.length} pages, ${unexplained} unexplained, ${defects} recorded ` +
      `defects; report written to ${relative(ROOT, out)}.`,
  );
  process.exitCode = unexplained > 0 ? EXIT_UNEXPLAINED : 0;
} catch (error) {
  console.error(`seo-diff: ${error.message}`);
  process.exitCode = EXIT_UNREADABLE;
}
