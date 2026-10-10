import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { afterEach, beforeEach, describe, it } from 'node:test';

const SCRIPT = fileURLToPath(new URL('../check-pinned-actions.mjs', import.meta.url));
const SHA = '3d3c42e5aac5ba805825da76410c181273ba90b1';
const PINNED = `    steps:\n      - uses: actions/checkout@${SHA} # v7.0.1\n`;

function check(args = []) {
  return spawnSync(process.execPath, [SCRIPT, ...args], { encoding: 'utf8' });
}

describe('check-pinned-actions', () => {
  let root;
  const write = (path, text) => {
    mkdirSync(join(root, path, '..'), { recursive: true });
    writeFileSync(join(root, path), text);
  };

  beforeEach(() => {
    root = mkdtempSync(join(tmpdir(), 'lodb-pinned-actions-'));
  });
  afterEach(() => rmSync(root, { recursive: true, force: true }));

  it('passes when every reference is pinned', () => {
    write('.github/workflows/ci.yml', PINNED);
    write('.github/workflows/release.yaml', '    uses: ./.github/workflows/ci.yml\n');

    const result = check(['--root', root]);

    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /2 references in 2 files, pinned/);
  });

  it('fails on an unpinned reference and names its file and line', () => {
    write('.github/workflows/ci.yml', `${PINNED}      - uses: actions/setup-node@v7\n`);

    const result = check(['--root', root]);

    assert.equal(result.status, 1);
    assert.match(result.stderr, /ci\.yml:3: actions\/setup-node@v7: not pinned/);
    assert.match(result.stderr, /1 of 2 references break the pinning rules/);
  });

  it('reads the actions of the repository too', () => {
    write('.github/workflows/ci.yml', PINNED);
    write('.github/actions/setup/action.yml', '    - uses: actions/cache@v6\n');

    const result = check(['--root', root]);

    assert.equal(result.status, 1);
    assert.match(result.stderr, /action\.yml:1: actions\/cache@v6/);
  });

  it('fails when there is no workflow to read', () => {
    const result = check(['--root', root]);

    assert.equal(result.status, 1);
    assert.match(result.stderr, /no workflow under/);
  });

  it('passes on the workflows of this repository', () => {
    const result = check();

    assert.equal(result.status, 0, result.stderr);
  });
});
