import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { after, before, describe, it } from 'node:test';
import { PRIVATE_KEY_VARIABLE, PUBLIC_KEY_VARIABLE } from '../lib/key-from-environment.mjs';
import { testKeyPair, testZip } from './test-keys.mjs';

const TOOL = fileURLToPath(new URL('..', import.meta.url));
const URL_OF_BUNDLE = 'https://github.com/o/r/releases/download/android-v2.4.0/lodb-bundle.zip';
const pair = testKeyPair();

// Runs a script of the tool with only the given variables: the host's never leak in.
function run(script, { args = [], variables = {}, cwd }) {
  return spawnSync(process.execPath, [join(TOOL, script), ...args], {
    cwd,
    encoding: 'utf8',
    env: { PATH: process.env.PATH, ...variables },
  });
}

describe('live update tool', () => {
  let directory;
  const sign = ({ minimum = ['--minimum-native', '2.4.0'], variables }) =>
    run('sign-bundle.mjs', {
      args: [
        ...['--zip', 'bundle.zip', '--id', '2.4.0', '--url', URL_OF_BUNDLE],
        ...[...minimum, '--out', 'bundle.json'],
      ],
      variables,
      cwd: directory,
    });
  const signed = () => sign({ variables: { [PRIVATE_KEY_VARIABLE]: pair.privateKey } });
  const verified = () =>
    run('verify-bundle.mjs', {
      args: ['--zip', 'bundle.zip', '--descriptor', 'bundle.json'],
      variables: { [PUBLIC_KEY_VARIABLE]: pair.publicKey },
      cwd: directory,
    });

  before(() => {
    directory = mkdtempSync(join(tmpdir(), 'lodb-live-update-cli-'));
  });
  after(() => rmSync(directory, { recursive: true, force: true }));

  it('signs a bundle the verification then accepts', () => {
    writeFileSync(join(directory, 'bundle.zip'), testZip());

    const signing = signed();
    const descriptor = JSON.parse(readFileSync(join(directory, 'bundle.json'), 'utf8'));

    assert.equal(signing.status, 0, signing.stderr);
    assert.equal(descriptor.id, '2.4.0');
    assert.equal(descriptor.minimumNativeVersion, '2.4.0');
    assert.equal(verified().status, 0);
  });

  it('never prints the private key', () => {
    writeFileSync(join(directory, 'bundle.zip'), testZip());

    const signing = signed();

    assert.doesNotMatch(signing.stdout + signing.stderr, /PRIVATE KEY/);
  });

  it('rejects the bundle once its zip changed', () => {
    writeFileSync(join(directory, 'bundle.zip'), testZip());
    signed();
    writeFileSync(join(directory, 'bundle.zip'), testZip('tampered'));

    const verification = verified();

    assert.equal(verification.status, 1);
    assert.match(verification.stderr, /checksum/);
  });

  it('asks for what is missing, the private key by name', () => {
    writeFileSync(join(directory, 'bundle.zip'), testZip());

    const incomplete = sign({
      minimum: [],
      variables: { [PRIVATE_KEY_VARIABLE]: pair.privateKey },
    });
    const keyless = sign({ variables: {} });

    assert.equal(incomplete.status, 1);
    assert.match(incomplete.stderr, /missing --minimum-native/);
    assert.equal(keyless.status, 1);
    assert.match(keyless.stderr, /LODB_LIVE_UPDATE_PRIVATE_KEY is not set/);
  });

  it('prints the public half of the key for the app', () => {
    const printing = run('public-key.mjs', {
      variables: { [PRIVATE_KEY_VARIABLE]: pair.privateKey },
      cwd: directory,
    });

    assert.equal(printing.status, 0, printing.stderr);
    assert.equal(printing.stdout, pair.publicKey);
  });
});
