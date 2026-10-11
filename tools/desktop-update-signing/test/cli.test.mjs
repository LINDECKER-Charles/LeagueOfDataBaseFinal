import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { afterEach, beforeEach, describe, it } from 'node:test';
import { appPublicKeys, keyList } from '../lib/app-key.mjs';
import { PRIVATE_KEY_VARIABLE, readPrivateKey, rawPublicKey } from '../lib/feed-signature.mjs';
import { CHANNEL, FEED_NAME, testEntry, testFeed, testKeyPair, testPackage } from './test-keys.mjs';

const TOOL = fileURLToPath(new URL('..', import.meta.url));
const pair = testKeyPair();

// Runs a script of the tool with only the given variables: the host's never leak in.
function run(script, { args = [], variables = {}, cwd }) {
  return spawnSync(process.execPath, [join(TOOL, script), ...args], {
    cwd,
    encoding: 'utf8',
    env: { PATH: process.env.PATH, ...variables },
  });
}

describe('feed signing tool', () => {
  let releases;
  const folder = () => ['--releases', releases, '--channel', CHANNEL];
  const signing = (args = ['--public-key', pair.publicKey], privatePem = pair.privatePem) =>
    run('sign-feed.mjs', {
      args: [...folder(), '--version', '1.4.0', ...args],
      variables: { [PRIVATE_KEY_VARIABLE]: privatePem },
    });
  const verification = (...extra) =>
    run('verify-feed.mjs', { args: [...folder(), '--public-key', pair.publicKey, ...extra] });
  const writePackage = (version, content = testPackage(version)) =>
    writeFileSync(join(releases, testEntry(version).FileName), content);

  beforeEach(() => {
    releases = mkdtempSync(join(tmpdir(), 'lodb-feed-signing-'));
    // vpk pack lists N-1, fetched for the delta, next to N.
    writeFileSync(join(releases, FEED_NAME), testFeed([testEntry('1.3.0'), testEntry('1.4.0')]));
    writePackage('1.3.0');
    writePackage('1.4.0');
  });
  afterEach(() => rmSync(releases, { recursive: true, force: true }));

  it('signs the feed of the release, which the verification then accepts', () => {
    const signed = signing();
    const feed = JSON.parse(readFileSync(join(releases, FEED_NAME), 'utf8'));

    assert.equal(signed.status, 0, signed.stderr);
    assert.deepEqual(
      feed.Assets.map((asset) => asset.Version),
      ['1.4.0'],
    );
    const verified = verification('--packages');
    assert.equal(verified.status, 0, verified.stderr);
    assert.match(verified.stderr, /1 package\(s\) match/);
  });

  it('never prints the private key', () => {
    const signed = signing();

    assert.doesNotMatch(signed.stdout + signed.stderr, /PRIVATE KEY/);
  });

  it('refuses a signing key that is not the pair of a key the app trusts', () => {
    const signed = signing([]);

    assert.equal(signed.status, 1);
    assert.match(signed.stderr, /not the pair of a key the app trusts/);
    assert.ok(signed.stderr.includes(keyList(appPublicKeys())), signed.stderr);
  });

  // The release that brings a new key is signed with the old one, which both builds trust.
  it('signs with either key while a key is rotated', () => {
    const next = testKeyPair();
    const rotating = ['--public-key', keyList([next.publicKey, pair.publicKey])];

    const signed = signing(rotating);
    const verified = run('verify-feed.mjs', { args: [...folder(), ...rotating] });

    assert.equal(signed.status, 0, signed.stderr);
    assert.equal(verified.status, 0, verified.stderr);
    assert.equal(
      run('verify-feed.mjs', { args: [...folder(), rotating[0], next.publicKey] }).status,
      1,
    );
  });

  // Checked against its exact-case SHA-256, the package would pass the release checks while
  // the app, reading key names without case, would take the last one.
  it('signs no feed whose entry repeats a field under another case', () => {
    const entry = { ...testEntry('1.4.0'), sha256: testEntry('6.6.6').SHA256 };
    writeFileSync(join(releases, FEED_NAME), testFeed([entry]));

    const signed = signing();

    assert.equal(signed.status, 1);
    assert.match(signed.stderr, /unexpected keys: "sha256"/);
    assert.equal(existsSync(join(releases, `${FEED_NAME}.sig`)), false);
  });

  it('asks for the private key by name', () => {
    const signed = signing(undefined, '');

    assert.equal(signed.status, 1);
    assert.match(signed.stderr, new RegExp(PRIVATE_KEY_VARIABLE));
  });

  it('rejects the feed once it changed', () => {
    signing();
    const feed = readFileSync(join(releases, FEED_NAME));
    writeFileSync(join(releases, FEED_NAME), Buffer.concat([feed, Buffer.from(' ')]));

    const verified = verification();

    assert.equal(verified.status, 1);
    assert.match(verified.stderr, /does not hold/);
  });

  it('rejects a feed without signature', () => {
    const verified = verification();

    assert.equal(verified.status, 1);
    assert.match(verified.stderr, /has no signature/);
  });

  it('rejects a feed signed with another key', () => {
    const stranger = testKeyPair();
    signing(['--public-key', stranger.publicKey], stranger.privatePem);

    assert.equal(verification().status, 1);
  });

  it('rejects a package swapped after signing', () => {
    signing();
    writePackage('1.4.0', testPackage('6.6.6'));

    const verified = verification('--packages');

    assert.equal(verified.status, 1);
    assert.match(verified.stderr, /does not match the SHA-256/);
  });
});

describe('key generation', () => {
  let folder;

  beforeEach(() => {
    folder = mkdtempSync(join(tmpdir(), 'lodb-feed-key-'));
  });
  afterEach(() => rmSync(folder, { recursive: true, force: true }));

  it('writes the private key and prints only its public half', () => {
    const out = join(folder, 'secrets', 'feed-signing.pem');

    const generated = run('generate-key.mjs', { args: ['--out', out] });

    assert.equal(generated.status, 0, generated.stderr);
    const privateKey = readPrivateKey(readFileSync(out, 'utf8'));
    assert.equal(generated.stdout, `${rawPublicKey(privateKey)}\n`);
    assert.doesNotMatch(generated.stdout + generated.stderr, /PRIVATE KEY/);
  });

  it('never overwrites a key', () => {
    const out = join(folder, 'feed-signing.pem');
    writeFileSync(out, 'the release key');

    const generated = run('generate-key.mjs', { args: ['--out', out] });

    assert.equal(generated.status, 1);
    assert.equal(readFileSync(out, 'utf8'), 'the release key');
  });

  it('refuses a file inside a git work tree', () => {
    mkdirSync(join(folder, '.git'));

    const generated = run('generate-key.mjs', {
      args: ['--out', join(folder, 'keys', 'feed-signing.pem')],
    });

    assert.equal(generated.status, 1);
    assert.match(generated.stderr, /inside the git work tree/);
  });
});
