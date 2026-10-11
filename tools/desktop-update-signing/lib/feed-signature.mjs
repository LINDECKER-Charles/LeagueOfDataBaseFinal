// Signature of a desktop release feed (ADR 0008): Ed25519 (RFC 8032) over the bytes of
// releases.<channel>.json, preceded by a context line and the feed's file name. The app
// verifies it with the public key compiled into it (Updates/Engine/UpdateFeeds.cs) before it
// reads the feed, so the file name binds a signature to its channel: a feed signed for one
// RID or channel never passes for another's.
import { createPrivateKey, createPublicKey, sign, verify } from 'node:crypto';

/** The release secret: the PEM of the private key, from the environment only. */
export const PRIVATE_KEY_VARIABLE = 'LODB_DESKTOP_UPDATE_PRIVATE_KEY';

// Must match FeedSignature.Context in src/LoDb.Desktop/Updates/Engine/Signing.
const CONTEXT = 'lodb-desktop-feed-v1';
const KEY_TYPE = 'ed25519';
const RAW_KEY_BYTES = 32;
const SIGNATURE_BYTES = 64;
const BASE64 = /^[A-Za-z0-9+/]+={0,2}$/;

/** What is signed: the context, the feed's file name, then its bytes as published. */
export function feedMessage(fileName, feed) {
  return Buffer.concat([Buffer.from(`${CONTEXT}\n${fileName}\n`, 'utf8'), feed]);
}

/** The signing key, from its PEM (PKCS#8). Refuses anything but Ed25519. */
export function readPrivateKey(pem) {
  const key = createPrivateKey(pem);
  if (key.asymmetricKeyType !== KEY_TYPE) {
    throw new Error(`the private key is ${key.asymmetricKeyType}, an Ed25519 key is expected`);
  }
  return key;
}

/** The public half of a key, as the app holds it: the 32 raw bytes in base64. */
export function rawPublicKey(key) {
  const publicKey = key.type === 'public' ? key : createPublicKey(key);
  const { x } = publicKey.export({ format: 'jwk' });
  return Buffer.from(x, 'base64url').toString('base64');
}

/** The verifying key, from the base64 of its 32 raw bytes (the app's form). */
export function readPublicKey(base64) {
  const raw = decodeBase64(String(base64).trim(), RAW_KEY_BYTES, 'public key');
  return createPublicKey({
    key: { kty: 'OKP', crv: 'Ed25519', x: raw.toString('base64url') },
    format: 'jwk',
  });
}

/** Signs the feed exactly as it will be published: any byte changed breaks it. */
export function signFeed({ fileName, feed, privateKey }) {
  return sign(null, feedMessage(fileName, feed), privateKey).toString('base64');
}

/** The content of releases.<channel>.json.sig: the signature in base64, one line. */
export function signatureFile(signature) {
  return `${signature}\n`;
}

/** Whether the signature file holds for the feed, as the app decides it. */
export function signatureHolds({ fileName, feed, signature, publicKey }) {
  let raw;
  try {
    raw = decodeBase64(String(signature).trim(), SIGNATURE_BYTES, 'signature');
  } catch {
    return false;
  }
  return verify(null, feedMessage(fileName, feed), publicKey, raw);
}

function decodeBase64(text, length, role) {
  const raw = BASE64.test(text) ? Buffer.from(text, 'base64') : Buffer.alloc(0);
  if (raw.length !== length) {
    throw new Error(`the ${role} must be ${length} bytes in base64`);
  }
  return raw;
}
