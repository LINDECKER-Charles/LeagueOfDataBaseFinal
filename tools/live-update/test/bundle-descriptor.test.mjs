import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { describe, it } from 'node:test';
import {
  checkDescriptor,
  checksumOf,
  describeBundle,
  verifyBundle,
} from '../lib/bundle-descriptor.mjs';
import { readPrivateKey, readPublicKey } from '../lib/bundle-signature.mjs';
import { testKeyPair, testZip } from './test-keys.mjs';

const pair = testKeyPair();
const privateKey = readPrivateKey(pair.privateKey);
const publicKey = readPublicKey(pair.publicKey);
const BUNDLE = {
  id: '2.4.0',
  url: 'https://github.com/o/r/releases/download/android-v2.4.0/lodb-bundle-2.4.0.zip',
  minimumNativeVersion: '2.4.0',
};

describe('describeBundle', () => {
  it('describes the zip as client-policy publish takes it', () => {
    const zip = testZip();

    const descriptor = describeBundle({ zip, bundle: BUNDLE, privateKey });

    assert.deepEqual(Object.keys(descriptor), [
      'id',
      'url',
      'checksum',
      'signature',
      'minimumNativeVersion',
    ]);
    assert.equal(descriptor.checksum, createHash('sha256').update(zip).digest('hex'));
    assert.equal(verifyBundle({ zip, descriptor, publicKey }), descriptor);
  });

  it('refuses a bundle the API would refuse', () => {
    const zip = testZip();

    assert.throws(
      () => describeBundle({ zip, bundle: { ...BUNDLE, id: 'public' }, privateKey }),
      /bundle id/,
    );
  });
});

describe('verifyBundle', () => {
  const zip = testZip();
  const descriptor = describeBundle({ zip, bundle: BUNDLE, privateKey });

  it('refuses another zip than the one described', () => {
    assert.throws(
      () => verifyBundle({ zip: testZip('tampered'), descriptor, publicKey }),
      /checksum/,
    );
  });

  it('refuses a zip described and signed by another key', () => {
    const forged = describeBundle({
      zip,
      bundle: BUNDLE,
      privateKey: readPrivateKey(testKeyPair().privateKey),
    });

    assert.throws(() => verifyBundle({ zip, descriptor: forged, publicKey }), /signature/);
  });

  it('refuses a checksum made to match a tampered zip', () => {
    const tampered = testZip('tampered');

    assert.throws(
      () =>
        verifyBundle({
          zip: tampered,
          descriptor: { ...descriptor, checksum: checksumOf(tampered) },
          publicKey,
        }),
      /signature/,
    );
  });
});

describe('checkDescriptor', () => {
  const valid = describeBundle({ zip: testZip(), bundle: BUNDLE, privateKey });

  const refused = [
    ['the id of the embedded bundle', { id: 'public' }],
    ['an id with a slash', { id: '2.4/0' }],
    ['an id beyond 64 characters', { id: 'a'.repeat(65) }],
    ['an http URL', { url: 'http://github.com/o/r/lodb-bundle.zip' }],
    ['a URL beyond 2048 characters', { url: `https://github.com/${'a'.repeat(2048)}` }],
    ['an upper case checksum', { checksum: 'A'.repeat(64) }],
    ['a signature with a line break', { signature: `${valid.signature.slice(0, 64)}\nAA==` }],
    ['a signature beyond 1024 characters', { signature: 'A'.repeat(1025) }],
    ['a pre-release minimum', { minimumNativeVersion: '2.4.0-beta.1' }],
    ['a missing minimum', { minimumNativeVersion: undefined }],
  ];

  for (const [name, change] of refused) {
    it(`refuses ${name}`, () => {
      assert.throws(() => checkDescriptor({ ...valid, ...change }), /invalid bundle/);
    });
  }

  it('refuses no descriptor at all', () => {
    assert.throws(() => checkDescriptor(null), /invalid bundle id/);
  });
});
