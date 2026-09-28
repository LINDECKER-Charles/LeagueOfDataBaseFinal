import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { after, describe, it } from 'node:test';
import { apkManifest, checkApk, sha256Of } from '../lib/apk-manifest.mjs';

const CLI = fileURLToPath(new URL('../publish/latest-manifest.mjs', import.meta.url));
const URL_OF_APK = 'https://github.com/o/r/releases/download/android-v2.4.0/lodb-2.4.0.apk';
const APK = Buffer.from('PK\u0003\u0004 an APK');

describe('apkManifest', () => {
  it('describes the APK as the app reads latest.json', () => {
    assert.deepEqual(
      apkManifest({ versionName: '2.4.0', url: URL_OF_APK, sha256: sha256Of(APK) }),
      {
        versionCode: 2_004_000,
        versionName: '2.4.0',
        url: URL_OF_APK,
        sha256: sha256Of(APK),
      },
    );
  });

  it('refuses an APK the app would not open', () => {
    const sha256 = sha256Of(APK);
    for (const url of ['http://github.com/o/r/a.apk', 'https://evil.example/a.apk']) {
      assert.throws(() => apkManifest({ versionName: '2.4.0', url, sha256 }), /release asset/);
    }
    assert.throws(() => apkManifest({ versionName: '2.4.0', url: URL_OF_APK, sha256: 'AB' }));
    assert.throws(() => apkManifest({ versionName: 'v2.4.0', url: URL_OF_APK, sha256 }));
  });

  it('checks an APK against the manifest', () => {
    const manifest = apkManifest({ versionName: '2.4.0', url: URL_OF_APK, sha256: sha256Of(APK) });

    assert.doesNotThrow(() => checkApk(manifest, APK));
    assert.throws(() => checkApk(manifest, Buffer.from('another APK')), /SHA-256/);
  });
});

describe('publish/latest-manifest.mjs', () => {
  const directory = mkdtempSync(join(tmpdir(), 'lodb-latest-manifest-'));
  after(() => rmSync(directory, { recursive: true, force: true }));
  const run = (...args) =>
    spawnSync(process.execPath, [CLI, ...args], { cwd: directory, encoding: 'utf8' });

  it('writes latest.json, then verifies the APK downloaded back', () => {
    writeFileSync(join(directory, 'lodb.apk'), APK);

    const writing = run(
      '--apk',
      'lodb.apk',
      '--version',
      '2.4.0',
      '--url',
      URL_OF_APK,
      '--out',
      'latest.json',
    );
    const verification = run('--verify', 'latest.json', '--apk', 'lodb.apk');

    assert.equal(writing.status, 0, writing.stderr);
    assert.equal(
      JSON.parse(readFileSync(join(directory, 'latest.json'), 'utf8')).versionCode,
      2_004_000,
    );
    assert.equal(verification.status, 0, verification.stderr);
  });

  it('refuses an APK changed on the way', () => {
    writeFileSync(join(directory, 'lodb.apk'), APK);
    run('--apk', 'lodb.apk', '--version', '2.4.0', '--url', URL_OF_APK, '--out', 'latest.json');
    writeFileSync(join(directory, 'downloaded.apk'), Buffer.from('another APK'));

    const verification = run('--verify', 'latest.json', '--apk', 'downloaded.apk');

    assert.equal(verification.status, 1);
    assert.match(verification.stderr, /SHA-256/);
  });
});
