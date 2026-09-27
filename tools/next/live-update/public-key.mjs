// Prints the public half of the bundle signing key, for the app's build
// (LODB_LIVE_UPDATE_PUBLIC_KEY, the `publicKey` of the LiveUpdate plugin):
//
//   LODB_LIVE_UPDATE_PRIVATE_KEY="$(cat /secure/lodb-live-update.pem)" \
//   node tools/next/live-update/public-key.mjs > lodb-live-update.pub.pem
//
// Only the public key may leave the release host: every copy of the app carries it.
import { publicKeyPem, readPrivateKey } from './lib/bundle-signature.mjs';
import { PRIVATE_KEY_VARIABLE, keyFromEnvironment } from './lib/key-from-environment.mjs';

try {
  process.stdout.write(publicKeyPem(readPrivateKey(keyFromEnvironment(PRIVATE_KEY_VARIABLE))));
} catch (error) {
  console.error(`public-key: ${error.message}`);
  process.exitCode = 1;
}
