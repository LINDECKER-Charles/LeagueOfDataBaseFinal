import assert from 'node:assert/strict';
import { generateKeyPairSync } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { describe, it } from 'node:test';
import {
  feedMessage,
  rawPublicKey,
  readPrivateKey,
  readPublicKey,
  signFeed,
  signatureFile,
  signatureHolds,
} from '../lib/feed-signature.mjs';
import { FEED_NAME, testEntry, testFeed, testKeyPair } from './test-keys.mjs';

const pair = testKeyPair();
const publicKey = readPublicKey(pair.publicKey);
const feed = testFeed([testEntry('1.4.0')]);
// Signed by this tool with a throwaway key, and checked by the app's tests as well
// (FeedSignatureTests): both sides agree on the signed message.
const FIXTURES = new URL(
  '../../../tests/LoDb.Desktop.Tests/Updates/Signing/Fixtures/',
  import.meta.url,
);

const signed = (fileName = FEED_NAME, privateKey = pair.privateKey) =>
  signFeed({ fileName, feed, privateKey });
const holds = (options) => signatureHolds({ fileName: FEED_NAME, feed, publicKey, ...options });

describe('feed signature', () => {
  it('holds for the feed it signed, with the public half of the key', () => {
    assert.equal(holds({ signature: signed() }), true);
  });

  it('breaks when a single byte of the feed changes', () => {
    const tampered = Buffer.from(feed);
    tampered[tampered.length - 2] ^= 1;

    assert.equal(holds({ signature: signed(), feed: tampered }), false);
  });

  it('breaks with the key of anyone else', () => {
    assert.equal(holds({ signature: signed(FEED_NAME, testKeyPair().privateKey) }), false);
  });

  it('binds the signature to the feed of its channel', () => {
    const beta = 'releases.win-x64-beta.json';

    assert.equal(holds({ signature: signed(beta) }), false);
    assert.equal(holds({ signature: signed(beta), fileName: beta }), true);
  });

  it('refuses a signature it cannot decode', () => {
    for (const signature of ['', 'not a signature', 'AAAA', `${signed()}AAAA`]) {
      assert.equal(holds({ signature }), false, signature);
    }
  });

  it('writes the signature file as one base64 line', () => {
    const file = signatureFile(signed());

    assert.match(file, /^[A-Za-z0-9+/]{86}==\n$/);
    assert.equal(holds({ signature: file }), true);
  });

  it('signs the context line, the file name, then the feed', () => {
    assert.deepEqual(
      feedMessage(FEED_NAME, Buffer.from('{}')),
      Buffer.from(`lodb-desktop-feed-v1\n${FEED_NAME}\n{}`),
    );
  });

  it('holds for the fixture the app verifies', () => {
    const fixture = (name) => readFileSync(new URL(name, FIXTURES));

    const holdsForFixture = signatureHolds({
      fileName: FEED_NAME,
      feed: fixture(FEED_NAME),
      signature: fixture(`${FEED_NAME}.sig`).toString('utf8'),
      publicKey: readPublicKey(fixture('public-key.txt').toString('utf8')),
    });

    assert.equal(holdsForFixture, true);
  });
});

describe('keys', () => {
  it('gives the public half as the 32 raw bytes the app decodes', () => {
    assert.equal(Buffer.from(pair.publicKey, 'base64').length, 32);
    assert.equal(rawPublicKey(readPrivateKey(pair.privatePem)), pair.publicKey);
  });

  it('refuses a private key that is not Ed25519', () => {
    const rsa = generateKeyPairSync('rsa', { modulusLength: 2048 });
    const pem = rsa.privateKey.export({ type: 'pkcs8', format: 'pem' });

    assert.throws(() => readPrivateKey(pem), /Ed25519 key is expected/);
  });

  it('refuses a public key that is not 32 bytes', () => {
    assert.throws(() => readPublicKey(Buffer.alloc(31).toString('base64')), /32 bytes/);
    assert.throws(() => readPublicKey('-----BEGIN PUBLIC KEY-----'), /32 bytes/);
  });
});
