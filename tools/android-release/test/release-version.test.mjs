import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { after, describe, it } from 'node:test';
import { isReleaseVersion, releaseOf, versionCodeOf } from '../lib/release-version.mjs';

const CLI = fileURLToPath(new URL('../build/release-version.mjs', import.meta.url));

describe('versionCodeOf', () => {
  it('orders the versionCodes as the versions', () => {
    assert.equal(versionCodeOf('2.4.0'), 2_004_000);
    assert.equal(versionCodeOf('2.3.999'), 2_003_999);
    assert.equal(versionCodeOf('10.0.1'), 10_000_001);
    assert.ok(versionCodeOf('2.10.0') > versionCodeOf('2.9.99'));
  });

  it('stays below the ceiling of Play', () => {
    assert.equal(versionCodeOf('2100.0.0'), 2_100_000_000);
    assert.throws(() => versionCodeOf('2100.0.1'), /outside/);
  });

  it('refuses what it cannot number', () => {
    for (const version of ['0.0.0', '2.1000.0', '2.4.1000', '2.04.0', 'v2.4.0', '2.4.0-beta.1']) {
      assert.throws(() => versionCodeOf(version), undefined, version);
    }
    assert.equal(isReleaseVersion('2.4'), false);
  });
});

describe('releaseOf', () => {
  it('releases the version of the android tag of the commit', () => {
    assert.deepEqual(releaseOf(['desktop-v3.0.0', 'android-v2.4.0']), {
      release: true,
      version: '2.4.0',
      versionCode: 2_004_000,
      tag: 'android-v2.4.0',
    });
  });

  it('releases nothing without a release tag', () => {
    assert.equal(releaseOf(['android-v2.4.0-rc.1', 'android-vnext']).release, false);
  });

  it('refuses two release tags on one commit', () => {
    assert.throws(() => releaseOf(['android-v2.4.0', 'android-v2.4.1']), /several/);
  });
});

describe('build/release-version.mjs', () => {
  const repository = mkdtempSync(join(tmpdir(), 'lodb-android-release-'));
  after(() => rmSync(repository, { recursive: true, force: true }));
  const git = (...args) =>
    execFileSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@example.test', ...args], {
      cwd: repository,
      encoding: 'utf8',
    }).trim();
  git('init', '--quiet');
  git('commit', '--quiet', '--allow-empty', '--no-gpg-sign', '-m', 'release');
  const sha = git('rev-parse', 'HEAD');
  const run = (...args) =>
    spawnSync(process.execPath, [CLI, ...args], { cwd: repository, encoding: 'utf8' });

  it('prints nothing to release without a tag', () => {
    assert.equal(
      run('--sha', sha).stdout,
      `release=false\nreason=no android-vX.Y.Z tag on ${sha}\n`,
    );
  });

  it('prints the version of the tag, for $GITHUB_OUTPUT', () => {
    git('tag', 'android-v2.4.0', sha);

    assert.equal(
      run('--sha', sha).stdout,
      'release=true\nversion=2.4.0\nversion_code=2004000\ntag=android-v2.4.0\n',
    );
  });

  it('refuses a short SHA', () => {
    const result = run('--sha', sha.slice(0, 7));

    assert.equal(result.status, 1);
    assert.match(result.stderr, /full commit SHA/);
  });
});
