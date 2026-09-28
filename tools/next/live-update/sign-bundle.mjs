// Signs a live update bundle (the zip of the `shell-store` build) and writes its descriptor,
// the `--bundle-*` values of `client-policy publish --platform android`:
//
//   LODB_LIVE_UPDATE_PRIVATE_KEY="$(cat /secure/lodb-live-update.pem)" \
//   node tools/next/live-update/sign-bundle.mjs --zip lodb-bundle-2.4.0.zip --id 2.4.0 \
//        --url https://…/lodb-bundle-2.4.0.zip --minimum-native 2.4.0 [--out bundle.json]
//
// The private key comes from the environment only. The signature is checked against the
// key's public half before anything is written, as the device will check it.
import { readFileSync, writeFileSync } from 'node:fs';
import { parseArgs } from 'node:util';
import { describeBundle, verifyBundle } from './lib/bundle-descriptor.mjs';
import { publicKeyPem, readPrivateKey, readPublicKey } from './lib/bundle-signature.mjs';
import { PRIVATE_KEY_VARIABLE, keyFromEnvironment } from './lib/key-from-environment.mjs';

const REQUIRED = ['zip', 'id', 'url', 'minimum-native'];

function readOptions() {
  const names = [...REQUIRED, 'out'];
  const { values } = parseArgs({
    options: Object.fromEntries(names.map((name) => [name, { type: 'string' }])),
  });
  const missing = REQUIRED.filter((name) => !values[name]);
  if (missing.length > 0) {
    throw new Error(`missing ${missing.map((name) => `--${name}`).join(', ')}`);
  }
  return values;
}

try {
  const values = readOptions();
  const privateKey = readPrivateKey(keyFromEnvironment(PRIVATE_KEY_VARIABLE));
  const zip = readFileSync(values.zip);
  const descriptor = describeBundle({
    zip,
    bundle: { id: values.id, url: values.url, minimumNativeVersion: values['minimum-native'] },
    privateKey,
  });
  verifyBundle({ zip, descriptor, publicKey: readPublicKey(publicKeyPem(privateKey)) });
  const json = `${JSON.stringify(descriptor, null, 2)}\n`;
  if (values.out) {
    writeFileSync(values.out, json);
    console.error(`sign-bundle: signed ${values.zip} as bundle ${descriptor.id} in ${values.out}`);
  } else {
    process.stdout.write(json);
  }
} catch (error) {
  console.error(`sign-bundle: ${error.message}`);
  process.exitCode = 1;
}
