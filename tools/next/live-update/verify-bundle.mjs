// Checks a live update bundle against its descriptor as the device will: the SHA-256 of the
// zip, then its RSA signature against the public key the app embeds.
//
//   LODB_LIVE_UPDATE_PUBLIC_KEY="$(cat lodb-live-update.pub.pem)" \
//   node tools/next/live-update/verify-bundle.mjs --zip lodb-bundle-2.4.0.zip \
//        --descriptor bundle.json
//
// The release runs it on the asset it downloads back from the GitHub Release, so that what
// the policy points at is what was signed.
import { readFileSync } from 'node:fs';
import { parseArgs } from 'node:util';
import { verifyBundle } from './lib/bundle-descriptor.mjs';
import { readPublicKey } from './lib/bundle-signature.mjs';
import { PUBLIC_KEY_VARIABLE, keyFromEnvironment } from './lib/key-from-environment.mjs';

try {
  const { values } = parseArgs({
    options: { zip: { type: 'string' }, descriptor: { type: 'string' } },
  });
  if (!values.zip || !values.descriptor) {
    throw new Error('--zip and --descriptor are required');
  }
  const descriptor = verifyBundle({
    zip: readFileSync(values.zip),
    descriptor: JSON.parse(readFileSync(values.descriptor, 'utf8')),
    publicKey: readPublicKey(keyFromEnvironment(PUBLIC_KEY_VARIABLE)),
  });
  console.error(`verify-bundle: bundle ${descriptor.id} holds (${values.zip})`);
} catch (error) {
  console.error(`verify-bundle: ${error.message}`);
  process.exitCode = 1;
}
