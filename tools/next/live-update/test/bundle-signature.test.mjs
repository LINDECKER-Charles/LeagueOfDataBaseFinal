import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createPrivateKey, generateKeyPairSync } from 'node:crypto';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, describe, it } from 'node:test';
import {
  publicKeyPem,
  readPrivateKey,
  readPublicKey,
  signBundle,
  signatureHolds,
} from '../lib/bundle-signature.mjs';
import { testKeyPair, testZip } from './test-keys.mjs';

const MAX_SIGNATURE_LENGTH = 1024;
const pair = testKeyPair();
const privateKey = readPrivateKey(pair.privateKey);
const publicKey = readPublicKey(pair.publicKey);
const hasOpenssl = spawnSync('openssl', ['version']).status === 0;

describe('bundle signature', () => {
  const directory = mkdtempSync(join(tmpdir(), 'lodb-live-update-'));
  after(() => rmSync(directory, { recursive: true, force: true }));

  it('holds for the zip it signed, with the public half of the key', () => {
    const zip = testZip();

    assert.equal(signatureHolds({ zip, signature: signBundle(zip, privateKey), publicKey }), true);
  });

  it('breaks when a single byte of the zip changes', () => {
    const zip = testZip();
    const signature = signBundle(zip, privateKey);
    zip[zip.length - 1] ^= 1;

    assert.equal(signatureHolds({ zip, signature, publicKey }), false);
  });

  it('breaks with the key of anyone else', () => {
    const zip = testZip();
    const stranger = readPrivateKey(testKeyPair().privateKey);

    assert.equal(signatureHolds({ zip, signature: signBundle(zip, stranger), publicKey }), false);
  });

  it('refuses a signature the plugin could not decode', () => {
    const zip = testZip();
    const signature = signBundle(zip, privateKey);

    assert.equal(signatureHolds({ zip, signature: `${signature}\n`, publicKey }), false);
    assert.equal(
      signatureHolds({ zip, signature: signature.replaceAll('+', '-'), publicKey }),
      false,
    );
  });

  // openssl's RSA default is PKCS#1 v1.5: the scheme of Java's SHA256withRSA on the device.
  it('holds for openssl as it does for SHA256withRSA', { skip: !hasOpenssl }, () => {
    const zip = testZip();
    writeFileSync(join(directory, 'bundle.zip'), zip);
    writeFileSync(
      join(directory, 'bundle.sig'),
      Buffer.from(signBundle(zip, privateKey), 'base64'),
    );
    writeFileSync(join(directory, 'public.pem'), publicKeyPem(privateKey));

    const openssl = spawnSync(
      'openssl',
      ['dgst', '-sha256', '-verify', 'public.pem', '-signature', 'bundle.sig', 'bundle.zip'],
      { cwd: directory, encoding: 'utf8' },
    );

    assert.equal(openssl.status, 0, openssl.stderr);
    assert.match(openssl.stdout, /Verified OK/);
  });

  it('fits the signature of a 4096 bits key in what the API stores', () => {
    const large = readPrivateKey(testKeyPair(4096).privateKey);

    assert.ok(signBundle(testZip(), large).length <= MAX_SIGNATURE_LENGTH);
  });
});

describe('keys', () => {
  it('reads the private key in PKCS#1 as in PKCS#8', () => {
    const pkcs1 = createPrivateKey(pair.privateKey).export({ type: 'pkcs1', format: 'pem' });

    assert.equal(readPrivateKey(pkcs1).asymmetricKeyType, 'rsa');
  });

  it('refuses a private key where the public one goes, since the app ships it', () => {
    assert.throws(() => readPublicKey(pair.privateKey), /BEGIN PUBLIC KEY/);
  });

  it('refuses a key the plugin cannot verify with', () => {
    const ec = generateKeyPairSync('ec', { namedCurve: 'P-256' });
    const pem = ec.privateKey.export({ type: 'pkcs8', format: 'pem' });

    assert.throws(() => readPrivateKey(pem), /not an RSA key/);
  });

  it('refuses an RSA key too short to be safe', () => {
    assert.throws(() => readPrivateKey(testKeyPair(1024).privateKey), /1024 bits/);
  });

  it('gives the public half as the X.509 PEM the plugin decodes', () => {
    assert.equal(publicKeyPem(privateKey), pair.publicKey);
  });
});
