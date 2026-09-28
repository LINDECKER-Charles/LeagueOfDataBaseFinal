// Throwaway RSA keys, generated for each run of the tests: no key, not even a test one,
// lives in the repository, so none can be mistaken for the release key.
import { generateKeyPairSync } from 'node:crypto';

const TEST_KEY_BITS = 2048;

/** A fresh RSA key pair, both halves in PEM. */
export function testKeyPair(modulusLength = TEST_KEY_BITS) {
  return generateKeyPairSync('rsa', {
    modulusLength,
    publicKeyEncoding: { type: 'spki', format: 'pem' },
    privateKeyEncoding: { type: 'pkcs8', format: 'pem' },
  });
}

/** Stands for the zip of a `shell-store` build: bytes only matter to the signature. */
export function testZip(content = 'index.html of the bundle') {
  return Buffer.from(`PK\u0003\u0004${content}`);
}
