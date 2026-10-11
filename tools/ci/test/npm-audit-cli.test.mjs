import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { afterEach, beforeEach, describe, it } from 'node:test';

const SCRIPT = fileURLToPath(new URL('../check-npm-audit.mjs', import.meta.url));
const TODAY = '2026-10-11';
const GHSA = 'GHSA-68fv-2mgg-jv7q';
const REPORT = {
  vulnerabilities: {
    'source-map-js': {
      via: [
        {
          source: 1241209,
          name: 'source-map-js',
          title: 'event-loop denial of service',
          url: `https://github.com/advisories/${GHSA}`,
          severity: 'high',
        },
      ],
    },
  },
};
const ENTRY = {
  advisory: GHSA,
  package: 'source-map-js',
  reason: 'reached only through the build-time CSS inliner',
  expires: '2026-11-30',
};

describe('check-npm-audit', () => {
  let dir;
  const write = (name, value) => {
    const path = join(dir, name);
    writeFileSync(path, typeof value === 'string' ? value : JSON.stringify(value));
    return path;
  };
  const check = ({ report = REPORT, allowList = [], args = [] } = {}) =>
    spawnSync(
      process.execPath,
      [SCRIPT, '--report', write('report.json', report), '--allowlist',
        write('allow.json', allowList), '--today', TODAY, ...args],
      { encoding: 'utf8' },
    );

  beforeEach(() => {
    dir = mkdtempSync(join(tmpdir(), 'lodb-npm-audit-'));
  });
  afterEach(() => rmSync(dir, { recursive: true, force: true }));

  it('fails on an advisory the allow-list does not accept, and names it', () => {
    const result = check();

    assert.equal(result.status, 1);
    assert.match(result.stderr, new RegExp(`source-map-js: ${GHSA} \\(high\\).*not allowed`));
    assert.match(result.stderr, /1 advisories at or above high block/);
  });

  it('passes when an entry accepts it, and prints until when', () => {
    const result = check({ allowList: [ENTRY] });

    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, new RegExp(`allowed until 2026-11-30: source-map-js: ${GHSA}`));
  });

  it('fails once the entry expired', () => {
    const result = check({ allowList: [{ ...ENTRY, expires: '2026-10-01' }] });

    assert.equal(result.status, 1);
    assert.match(result.stderr, /allowed until 2026-10-01, expired/);
  });

  it('fails on a malformed allow-list, which then accepts nothing', () => {
    const result = check({ allowList: [{ ...ENTRY, reason: '' }] });

    assert.equal(result.status, 1);
    assert.match(result.stderr, /entry 0: missing reason/);
    assert.match(result.stderr, /not allowed/);
  });

  it('warns about an entry that names nothing reported', () => {
    const result = check({ report: { vulnerabilities: {} }, allowList: [ENTRY] });

    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /::warning::.*GHSA-68fv-2mgg-jv7q .* remove the entry/);
  });

  it('follows the audit level', () => {
    assert.equal(check({ args: ['--audit-level', 'critical'] }).status, 0);
    assert.match(check({ args: ['--audit-level', 'severe'] }).stderr, /must be one of/);
  });

  it('fails when npm gave no report', () => {
    const result = check({ report: 'npm ERR! network' });

    assert.equal(result.status, 1);
    assert.match(result.stderr, /npm audit gave no JSON report/);
  });

  it('fails when npm reported an error', () => {
    const result = check({ report: { error: { summary: 'audit endpoint returned 503' } } });

    assert.equal(result.status, 1);
    assert.match(result.stderr, /npm audit failed: audit endpoint returned 503/);
  });
});
