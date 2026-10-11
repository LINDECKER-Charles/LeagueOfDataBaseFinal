// Fails when a production dependency of the web app has an advisory at or above the audit level
// that the allow-list does not accept (lib/npm-audit.mjs):
//
//   node tools/ci/check-npm-audit.mjs [--prefix DIR] [--allowlist FILE] [--audit-level LEVEL]
//                                     [--report FILE] [--today YYYY-MM-DD]
//
// Runs `npm audit --omit=dev --package-lock-only --json` in DIR (src/LoDb.Web): the production
// dependencies are bundled into the SSR server and the browser; the development ones never
// leave the build. The lockfile is enough, no install is needed. --report reads a saved report
// instead; --today fixes the date the expiries are compared with (tests).
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { SEVERITIES, allowListProblemsOf, auditOutcomeOf } from './lib/npm-audit.mjs';

const REPOSITORY = fileURLToPath(new URL('../..', import.meta.url));
const DEFAULTS = Object.freeze({
  prefix: join(REPOSITORY, 'src', 'LoDb.Web'),
  allowlist: join(REPOSITORY, 'tools', 'ci', 'npm-audit-allowlist.json'),
  'audit-level': 'high',
});
const AUDIT_ARGS = ['audit', '--omit=dev', '--package-lock-only', '--json'];
const IS_WINDOWS = process.platform === 'win32';
const MAX_REPORT_BYTES = 64 * 1024 * 1024;

function auditReportOf(prefix) {
  // npm exits non-zero whenever it finds an advisory: the report decides, not the status.
  const result = spawnSync(IS_WINDOWS ? 'npm.cmd' : 'npm', AUDIT_ARGS, {
    cwd: prefix,
    encoding: 'utf8',
    maxBuffer: MAX_REPORT_BYTES,
    shell: IS_WINDOWS,
  });
  if (result.error) {
    throw result.error;
  }
  return parseReport(result.stdout, result.stderr);
}

function parseReport(text, detail = '') {
  let report;
  try {
    report = JSON.parse(text);
  } catch {
    throw new Error(`npm audit gave no JSON report ${detail.trim()}`.trim());
  }
  if (report.error) {
    throw new Error(`npm audit failed: ${report.error.summary ?? JSON.stringify(report.error)}`);
  }
  return report;
}

const describe = (item) => `${item.package}: ${item.id} (${item.severity}) ${item.title}`;

function print(outcome, problems, allowlist) {
  for (const { index, problem } of problems) {
    console.error(`${allowlist}: entry ${index}: ${problem}`);
  }
  for (const item of outcome.blocking) {
    const why = item.expired ? `allowed until ${item.expired}, expired` : 'not allowed';
    console.error(`${describe(item)} — ${why} — ${item.url}`);
  }
  for (const item of outcome.allowed) {
    console.log(`allowed until ${item.expires}: ${describe(item)}`);
  }
  for (const entry of outcome.stale) {
    console.log(`::warning::${allowlist}: ${entry.advisory} (${entry.package}) is no longer` +
      ' reported at this level: remove the entry.');
  }
}

function optionsOf() {
  const { values } = parseArgs({
    options: {
      prefix: { type: 'string' },
      allowlist: { type: 'string' },
      'audit-level': { type: 'string' },
      report: { type: 'string' },
      today: { type: 'string' },
    },
  });
  const options = { ...DEFAULTS, ...values };
  if (!SEVERITIES.includes(options['audit-level'])) {
    throw new Error(`--audit-level must be one of ${SEVERITIES.join(', ')}`);
  }
  return { ...options, today: options.today ?? new Date().toISOString().slice(0, 10) };
}

function main() {
  const options = optionsOf();
  const level = options['audit-level'];
  const entries = JSON.parse(readFileSync(options.allowlist, 'utf8'));
  const problems = allowListProblemsOf(entries, options.today);
  const report = options.report
    ? parseReport(readFileSync(options.report, 'utf8'))
    : auditReportOf(options.prefix);
  // A malformed allow-list accepts nothing.
  const accepted = problems.length === 0 ? entries : [];
  const outcome = auditOutcomeOf(report, accepted, { level, today: options.today });
  print(outcome, problems, relative(process.cwd(), options.allowlist) || options.allowlist);
  if (problems.length > 0 || outcome.blocking.length > 0) {
    throw new Error(`${outcome.blocking.length} advisories at or above ${level} block, ` +
      `${problems.length} malformed allow-list entries`);
  }
  console.log(`check-npm-audit: no unaccepted advisory at or above ${level}` +
    ` (${outcome.allowed.length} allowed)`);
}

try {
  main();
} catch (error) {
  console.error(`check-npm-audit: ${error.message}`);
  process.exitCode = 1;
}
