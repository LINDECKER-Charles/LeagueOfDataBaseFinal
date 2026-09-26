// Writes the assetlinks.json that nginx serves (docker/next/nginx/server.d/assetlinks.conf)
// from the SHA-256 fingerprint of the app's signing certificate, given as a parameter:
//
//   node tools/next/android/assetlinks.mjs --fingerprint AB:CD:… [--fingerprint …]
//        [--package com.leagueofdatabase.app] [--out path/assetlinks.json]
//
// Without --out, the statement goes to standard output. The fingerprint of a keystore is
// printed by `keytool -list -v -keystore <file>`; Play's app signing key is in the Play
// Console (Setup > App integrity).
import { writeFileSync } from 'node:fs';
import { parseArgs } from 'node:util';
import { APP_PACKAGE, assetLinks } from './lib/asset-links.mjs';

try {
  const { values } = parseArgs({
    options: {
      fingerprint: { type: 'string', multiple: true },
      package: { type: 'string', default: APP_PACKAGE },
      out: { type: 'string' },
    },
  });
  const json = `${JSON.stringify(
    assetLinks({ packageName: values.package, fingerprints: values.fingerprint }),
    null,
    2,
  )}\n`;
  if (values.out) {
    writeFileSync(values.out, json);
    console.error(`assetlinks: wrote ${values.out}`);
  } else {
    process.stdout.write(json);
  }
} catch (error) {
  console.error(`assetlinks: ${error.message}`);
  process.exitCode = 1;
}
