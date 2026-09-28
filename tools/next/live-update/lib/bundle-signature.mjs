// Signature of a live update bundle (ADR 0008, L10.3): RSA PKCS#1 v1.5 over the SHA-256 of
// the zip's bytes, in base64. @capawesome/capacitor-live-update checks it on the device with
// `Signature.getInstance("SHA256withRSA")` against the X.509 public key the app embeds, and
// refuses the bundle before unpacking it when it does not hold.
import { constants, createPrivateKey, createPublicKey, sign, verify } from 'node:crypto';

const DIGEST = 'sha256';
const RSA_PKCS1 = { padding: constants.RSA_PKCS1_PADDING };
// Below 2048 bits RSA is no longer safe; above 4096 the base64 signature outgrows the 1024
// characters the API stores (ClientPolicyEntry.BundleSignatureMaxLength).
export const MIN_KEY_BITS = 2048;
export const MAX_KEY_BITS = 4096;
const PUBLIC_PEM_HEADER = '-----BEGIN PUBLIC KEY-----';
const SIGNATURE = /^[A-Za-z0-9+/]+={0,2}$/;

function checkRsa(key, role) {
  if (key.asymmetricKeyType !== 'rsa') {
    throw new Error(`the ${role} key is not an RSA key: the plugin verifies SHA256withRSA`);
  }
  const bits = key.asymmetricKeyDetails?.modulusLength ?? 0;
  if (bits < MIN_KEY_BITS || bits > MAX_KEY_BITS) {
    throw new Error(
      `the ${role} key has ${bits} bits, ${MIN_KEY_BITS} to ${MAX_KEY_BITS} expected`,
    );
  }
  return key;
}

/** The signing key, from its PEM (PKCS#8 or PKCS#1). Refuses anything but RSA 2048–4096. */
export function readPrivateKey(pem) {
  return checkRsa(createPrivateKey(pem), 'private');
}

/**
 * The verifying key, from its PEM (X.509 SubjectPublicKeyInfo, what the plugin decodes).
 * Refuses a private key: this one ships inside the app, where anyone can read it.
 */
export function readPublicKey(pem) {
  if (!String(pem).trimStart().startsWith(PUBLIC_PEM_HEADER)) {
    throw new Error(`the public key must be a PEM starting with ${PUBLIC_PEM_HEADER}`);
  }
  return checkRsa(createPublicKey(pem), 'public');
}

/** The public half of the signing key, as the app's configuration takes it. */
export function publicKeyPem(privateKey) {
  return createPublicKey(privateKey).export({ type: 'spki', format: 'pem' });
}

/** Signs the zip exactly as downloaded by the device: any byte changed breaks it. */
export function signBundle(zip, privateKey) {
  return sign(DIGEST, zip, { key: privateKey, ...RSA_PKCS1 }).toString('base64');
}

/** Whether the signature holds for the zip, as the plugin would decide it. */
export function signatureHolds({ zip, signature, publicKey }) {
  if (!SIGNATURE.test(signature)) {
    return false;
  }
  return verify(DIGEST, zip, { key: publicKey, ...RSA_PKCS1 }, Buffer.from(signature, 'base64'));
}
