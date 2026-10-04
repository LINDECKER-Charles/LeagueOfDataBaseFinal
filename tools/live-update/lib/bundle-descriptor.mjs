// The live update bundle as the client policy publishes it (`LiveUpdateBundle`, L9.0): the
// fields of `client-policy publish --bundle-*`. Checked with the rules of the API
// (PublishPolicyRules) and of the app (usable-bundle.ts), so that a release never hands over
// a descriptor the API would refuse or the app would ignore.
import { createHash } from 'node:crypto';
import { signBundle, signatureHolds } from './bundle-signature.mjs';

const MAX_URL_LENGTH = 2048;
const MAX_SIGNATURE_LENGTH = 1024;
// The plugin keeps the bundle embedded in the APK under this id.
const RESERVED_ID = 'public';

const RULES = [
  ['id', (id) => /^[A-Za-z0-9._-]{1,64}$/.test(id) && id !== RESERVED_ID],
  ['url', (url) => url.length <= MAX_URL_LENGTH && URL.parse(url)?.protocol === 'https:'],
  ['checksum', (checksum) => /^[0-9a-f]{64}$/.test(checksum)],
  [
    'signature',
    (signature) =>
      signature.length <= MAX_SIGNATURE_LENGTH && /^[A-Za-z0-9+/]+={0,2}$/.test(signature),
  ],
  ['minimumNativeVersion', (version) => /^\d{1,9}\.\d{1,9}\.\d{1,9}$/.test(version)],
];

/** SHA-256 of the zip in lower case hexadecimal, as the API stores the checksum. */
export function checksumOf(zip) {
  return createHash('sha256').update(zip).digest('hex');
}

/** Throws on the first field the API or the app would refuse; returns the descriptor. */
export function checkDescriptor(descriptor) {
  for (const [field, holds] of RULES) {
    const value = descriptor?.[field];
    if (typeof value !== 'string' || !holds(value)) {
      throw new Error(`invalid bundle ${field}: ${JSON.stringify(value)}`);
    }
  }
  return descriptor;
}

/**
 * Signs the zip and describes it. `bundle` holds `id`, `url` and `minimumNativeVersion`:
 * the oldest shell whose native plugins this front works with.
 */
export function describeBundle({ zip, bundle, privateKey }) {
  return checkDescriptor({
    id: bundle.id,
    url: bundle.url,
    checksum: checksumOf(zip),
    signature: signBundle(zip, privateKey),
    minimumNativeVersion: bundle.minimumNativeVersion,
  });
}

/** Throws unless the zip is the one the descriptor names, signed by the public key's pair. */
export function verifyBundle({ zip, descriptor, publicKey }) {
  checkDescriptor(descriptor);
  if (checksumOf(zip) !== descriptor.checksum) {
    throw new Error(`the zip does not match the checksum of bundle ${descriptor.id}`);
  }
  if (!signatureHolds({ zip, signature: descriptor.signature, publicKey })) {
    throw new Error(`the signature of bundle ${descriptor.id} does not hold`);
  }
  return descriptor;
}
