// Signs the feed of one RID's release folder, after vpk pack and before the update gate:
//
//   LODB_DESKTOP_UPDATE_PRIVATE_KEY="$(cat feed-signing.pem)" \
//   node tools/desktop-update-signing/sign-feed.mjs --releases DIR --channel win-x64 \
//        --version 1.4.0 [--public-key BASE64]
//
// Rewrites DIR/releases.<channel>.json as the GitHub Release of --version publishes it (its
// own packages only), then writes the signature next to it (releases.<channel>.json.sig).
// Refuses a signing key whose public half is not the key the app trusts: the one compiled
// into the app (UpdateFeeds.FeedPublicKey), or --public-key for a local E2E build that trusts
// a throwaway key. The private key comes from the environment only and is never printed.
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseArgs } from 'node:util';
import { keyFromEnvironment } from '../live-update/lib/key-from-environment.mjs';
import { appPublicKey } from './lib/app-key.mjs';
import {
  PRIVATE_KEY_VARIABLE,
  rawPublicKey,
  readPrivateKey,
  readPublicKey,
  signFeed,
  signatureFile,
  signatureHolds,
} from './lib/feed-signature.mjs';
import { feedOfVersion, readFeed } from './lib/release-feed.mjs';

const REQUIRED = ['releases', 'channel', 'version'];

function readOptions() {
  const names = [...REQUIRED, 'public-key'];
  const { values } = parseArgs({
    options: Object.fromEntries(names.map((name) => [name, { type: 'string' }])),
  });
  const missing = REQUIRED.filter((name) => !values[name]);
  if (missing.length > 0) {
    throw new Error(`missing ${missing.map((name) => `--${name}`).join(', ')}`);
  }
  return values;
}

function trustedSigningKey(expected) {
  const privateKey = readPrivateKey(keyFromEnvironment(PRIVATE_KEY_VARIABLE));
  if (rawPublicKey(privateKey) !== expected) {
    throw new Error(
      `${PRIVATE_KEY_VARIABLE} is not the pair of the key the app trusts (${expected})`,
    );
  }
  return privateKey;
}

try {
  const values = readOptions();
  const expected = values['public-key'] ?? appPublicKey();
  const privateKey = trustedSigningKey(expected);
  const { fileName, feed } = readFeed(values.releases, values.channel);
  const published = feedOfVersion(feed, values.version);
  const signature = signFeed({ fileName, feed: published, privateKey });
  const publicKey = readPublicKey(expected);
  if (!signatureHolds({ fileName, feed: published, signature, publicKey })) {
    throw new Error(`the signature of ${fileName} does not hold`);
  }
  writeFileSync(join(values.releases, fileName), published);
  writeFileSync(join(values.releases, `${fileName}.sig`), signatureFile(signature));
  console.error(`sign-feed: ${fileName} of ${values.version} signed in ${values.releases}`);
} catch (error) {
  console.error(`sign-feed: ${error.message}`);
  process.exitCode = 1;
}
