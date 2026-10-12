import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import {
  existsSync,
  mkdtempSync,
  readdirSync,
  readFileSync,
  rmSync,
  statSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

// The switch of the maintenance page (docker/nginx/server.d/maintenance.conf), as the deploy job
// turns it on a host, in a folder like the host's .deploy/maintenance.
const SCRIPT = fileURLToPath(new URL('../maintenance.sh', import.meta.url));
const ACTIVE = 'active.json';
const LAST_DURATION = 'last-duration';
// A window that started a minute and a half ago, as a deploy would have written it.
const ELAPSED_SECONDS = 90;
// The clock moves while the script runs: a measure within these seconds is the expected one.
const CLOCK_SLACK_SECONDS = 5;
// nginx reads the files as its own user, through a read-only mount.
const WORLD_READABLE = 0o644;
const WORLD_LISTABLE = 0o755;
const PERMISSIONS = 0o777;
const IS_POSIX = process.platform !== 'win32';
const workspaces = [];

after(() => workspaces.forEach((dir) => rmSync(dir, { recursive: true, force: true })));

function stateDir() {
  const root = mkdtempSync(join(tmpdir(), 'lodb-maintenance-'));
  workspaces.push(root);
  return join(root, '.deploy', 'maintenance');
}

function maintenance(command, dir) {
  const run = spawnSync('bash', [SCRIPT, command, dir], { encoding: 'utf8' });
  assert.equal(run.status, 0, run.stderr);
  return run;
}

const nowSeconds = () => Math.floor(Date.now() / 1000);
const activeOf = (dir) => JSON.parse(readFileSync(join(dir, ACTIVE), 'utf8'));
const lastDurationOf = (dir) => Number(readFileSync(join(dir, LAST_DURATION), 'utf8'));
const modeOf = (path) => statSync(path).mode & PERMISSIONS;

function startedAgo(dir, seconds) {
  const startedAt = nowSeconds() - seconds;
  writeFileSync(join(dir, ACTIVE), JSON.stringify({ startedAt }));
}

describe('maintenance.sh', () => {
  it('turns the page on with its start alone when no deploy was measured yet', () => {
    const dir = stateDir();
    const before = nowSeconds();

    maintenance('start', dir);

    const active = activeOf(dir);
    assert.deepEqual(Object.keys(active), ['startedAt']);
    assert.ok(active.startedAt >= before && active.startedAt <= nowSeconds(), active.startedAt);
  });

  it('expects the length of the previous window', () => {
    const dir = stateDir();
    maintenance('start', dir);
    startedAgo(dir, ELAPSED_SECONDS);
    maintenance('end', dir);

    maintenance('start', dir);

    const expected = activeOf(dir).expectedSeconds;
    assert.ok(Math.abs(expected - ELAPSED_SECONDS) <= CLOCK_SLACK_SECONDS, String(expected));
  });

  it('turns the page off and keeps how long it lasted', () => {
    const dir = stateDir();
    maintenance('start', dir);
    startedAgo(dir, ELAPSED_SECONDS);

    const run = maintenance('end', dir);

    assert.equal(existsSync(join(dir, ACTIVE)), false);
    const lasted = lastDurationOf(dir);
    assert.ok(Math.abs(lasted - ELAPSED_SECONDS) <= CLOCK_SLACK_SECONDS, String(lasted));
    assert.match(run.stdout, /off after \d+ s/);
  });

  it('measures at least a second, never a window of none', () => {
    const dir = stateDir();
    maintenance('start', dir);

    maintenance('end', dir);

    assert.ok(lastDurationOf(dir) >= 1);
  });

  it('does nothing at the end of a window that was never on', () => {
    const dir = stateDir();
    maintenance('start', dir);
    startedAgo(dir, ELAPSED_SECONDS);
    maintenance('end', dir);
    const lasted = readFileSync(join(dir, LAST_DURATION), 'utf8');

    maintenance('end', dir);

    assert.equal(readFileSync(join(dir, LAST_DURATION), 'utf8'), lasted);
  });

  it('ignores a previous duration it cannot read', () => {
    for (const unreadable of ['', 'soon', '0', '-12', '1.5']) {
      const dir = stateDir();
      maintenance('start', dir);
      writeFileSync(join(dir, LAST_DURATION), unreadable);

      maintenance('start', dir);

      assert.deepEqual(Object.keys(activeOf(dir)), ['startedAt'], unreadable);
    }
  });

  it('keeps the previous duration when the start of the window is unreadable', () => {
    const dir = stateDir();
    maintenance('start', dir);
    startedAgo(dir, ELAPSED_SECONDS);
    maintenance('end', dir);
    const lasted = readFileSync(join(dir, LAST_DURATION), 'utf8');
    writeFileSync(join(dir, ACTIVE), '{}');

    maintenance('end', dir);

    assert.equal(existsSync(join(dir, ACTIVE)), false);
    assert.equal(readFileSync(join(dir, LAST_DURATION), 'utf8'), lasted);
  });

  it('leaves its files readable by nginx and no temporary file behind', { skip: !IS_POSIX }, () => {
    const dir = stateDir();
    maintenance('start', dir);
    startedAgo(dir, ELAPSED_SECONDS);
    maintenance('end', dir);
    maintenance('start', dir);

    assert.equal(modeOf(dir), WORLD_LISTABLE);
    assert.equal(modeOf(join(dir, ACTIVE)), WORLD_READABLE);
    assert.equal(modeOf(join(dir, LAST_DURATION)), WORLD_READABLE);
    assert.deepEqual(readdirSync(dir).sort(), [ACTIVE, LAST_DURATION]);
  });

  it('refuses an unknown command and a missing folder', () => {
    for (const args of [['open', stateDir()], ['start']]) {
      const run = spawnSync('bash', [SCRIPT, ...args], { encoding: 'utf8' });

      assert.equal(run.status, 1, args.join(' '));
      assert.match(run.stderr, /usage/);
    }
  });
});
