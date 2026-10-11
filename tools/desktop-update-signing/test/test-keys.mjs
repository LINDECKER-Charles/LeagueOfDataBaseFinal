// Throwaway Ed25519 keys and release feeds, made for each run of the tests: no key, not even a
// test one, lives in the repository, so none can be mistaken for the release key.
import { createHash, generateKeyPairSync } from 'node:crypto';
import { rawPublicKey } from '../lib/feed-signature.mjs';

export const CHANNEL = 'win-x64';
export const FEED_NAME = `releases.${CHANNEL}.json`;

/** A fresh pair: the private key as an object and in PEM, the public one as the app holds it. */
export function testKeyPair() {
  const { privateKey, publicKey } = generateKeyPairSync('ed25519');
  return {
    privateKey,
    privatePem: privateKey.export({ type: 'pkcs8', format: 'pem' }),
    publicKey: rawPublicKey(publicKey),
  };
}

/** Stands for a nupkg: bytes only matter to the checksums. */
export function testPackage(version) {
  return Buffer.from(`package ${version}`);
}

/** The entry vpk writes for a package (Newtonsoft, PascalCase, upper case hexadecimal). */
export function testEntry(version, type = 'Full') {
  const content = testPackage(version);
  return {
    PackageId: 'LoDb.Desktop',
    Version: version,
    Type: type,
    FileName: `LoDb.Desktop-${version}-${CHANNEL}-${type.toLowerCase()}.nupkg`,
    SHA1: createHash('sha1').update(content).digest('hex').toUpperCase(),
    SHA256: createHash('sha256').update(content).digest('hex').toUpperCase(),
    Size: content.length,
    NotesMarkdown: '',
    NotesHTML: '',
  };
}

/** A feed as vpk pack writes it into a release folder. */
export function testFeed(entries) {
  return Buffer.from(JSON.stringify({ Assets: entries }), 'utf8');
}
