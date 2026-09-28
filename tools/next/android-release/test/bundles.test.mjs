import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { generateKeyPairSync } from 'node:crypto';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { after, before, describe, it } from 'node:test';
import { verifyBundle } from '../../live-update/lib/bundle-descriptor.mjs';
import { readPublicKey } from '../../live-update/lib/bundle-signature.mjs';

const SCRIPT = fileURLToPath(new URL('../build/bundles.sh', import.meta.url));
const BASE_URL = 'https://github.com/o/r/releases/download/android-v2.4.0';
// Throwaway key, generated for this run only.
const pair = generateKeyPairSync('rsa', { modulusLength: 2048 });
const publicKey = readPublicKey(pair.publicKey.export({ type: 'spki', format: 'pem' }));
const hasZip = spawnSync('zip', ['-v']).status === 0;

describe('build/bundles.sh', { skip: !hasZip }, () => {
  const directory = mkdtempSync(join(tmpdir(), 'lodb-bundles-'));
  const out = join(directory, 'bundles');
  const read = (name) => ({
    zip: readFileSync(join(out, `${name}.zip`)),
    descriptor: JSON.parse(readFileSync(join(out, `${name}.json`), 'utf8')),
  });
  const entries = (name) =>
    execFileSync('unzip', ['-Z1', join(out, `${name}.zip`)], { encoding: 'utf8' }).split('\n');
  let result;

  before(() => {
    mkdirSync(join(directory, 'web', 'media'), { recursive: true });
    writeFileSync(
      join(directory, 'web', 'index.html'),
      '<lodb-root></lodb-root><script src="main.js"></script>',
    );
    writeFileSync(join(directory, 'web', 'main.js'), 'bootstrapApplication();');
    writeFileSync(join(directory, 'web', 'media', 'logo.svg'), '<svg/>');
    result = spawnSync(
      'bash',
      [
        SCRIPT,
        ...['--web', join(directory, 'web'), '--version', '2.4.0', '--minimum-native', '2.3.0'],
        ...['--base-url', BASE_URL, '--out', out],
      ],
      {
        encoding: 'utf8',
        env: {
          PATH: process.env.PATH,
          LODB_LIVE_UPDATE_PRIVATE_KEY: pair.privateKey.export({ type: 'pkcs8', format: 'pem' }),
        },
      },
    );
  });
  after(() => rmSync(directory, { recursive: true, force: true }));

  it('signs the bundle of the release, index.html at the root of the zip', () => {
    assert.equal(result.status, 0, result.stderr);
    const release = read('lodb-bundle-2.4.0');

    assert.deepEqual(verifyBundle({ ...release, publicKey }), {
      ...release.descriptor,
      id: '2.4.0',
      url: `${BASE_URL}/lodb-bundle-2.4.0.zip`,
      minimumNativeVersion: '2.3.0',
    });
    assert.deepEqual(entries('lodb-bundle-2.4.0').filter(Boolean).sort(), [
      'index.html',
      'main.js',
      'media/',
      'media/logo.svg',
    ]);
  });

  it('signs a faulty bundle whose page never starts the app', () => {
    const faulty = read('gate/lodb-bundle-2.4.0-gate-faulty');

    assert.equal(verifyBundle({ ...faulty, publicKey }).id, '2.4.0-gate-faulty');
    assert.doesNotMatch(faulty.zip.toString('latin1'), /<script|lodb-root/);
    assert.deepEqual(entries('gate/lodb-bundle-2.4.0-gate-faulty').filter(Boolean), ['index.html']);
  });

  it('describes a tampered bundle that only its signature can refuse', () => {
    const tampered = read('gate/lodb-bundle-2.4.0-gate-tampered');

    assert.equal(tampered.descriptor.url, `${BASE_URL}/lodb-bundle-2.4.0-gate-tampered.zip`);
    assert.ok(entries('gate/lodb-bundle-2.4.0-gate-tampered').includes('tampered.txt'));
    assert.throws(() => verifyBundle({ ...tampered, publicKey }), /signature/);
  });

  it('signs nothing without the key', () => {
    const keyless = spawnSync(
      'bash',
      [
        SCRIPT,
        '--web',
        join(directory, 'web'),
        '--version',
        '2.4.1',
        '--minimum-native',
        '2.3.0',
        '--base-url',
        BASE_URL,
        '--out',
        join(directory, 'keyless'),
      ],
      { encoding: 'utf8', env: { PATH: process.env.PATH } },
    );

    assert.notEqual(keyless.status, 0);
    assert.match(keyless.stderr, /LODB_LIVE_UPDATE_PRIVATE_KEY is not set/);
  });
});
