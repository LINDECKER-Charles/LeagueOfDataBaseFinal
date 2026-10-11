// Signs the feed of one RID's release folder, after vpk pack and before the update gate:
//
//   LODB_DESKTOP_UPDATE_PRIVATE_KEY="$(cat feed-signing.pem)" \
//   node tools/desktop-update-signing/sign-feed.mjs --releases DIR --channel win-x64 \
//        --version 1.4.0 [--public-key "BASE64 ..."]
//
// Rewrites DIR/releases.<channel>.json as the GitHub Release of --version publishes it (its
// own packages only), then writes the signature next to it (releases.<channel>.json.sig).
// Refuses a signing key whose public half is not among the keys the app trusts: those
// compiled into the app (UpdateFeeds.FeedPublicKeys), or --public-key for a local E2E build
// that trusts a throwaway key. The private key comes from the environment only and is never
// printed.
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseArgs } from 'node:util';
import { keyFromEnvironment } from '../live-update/lib/key-from-environment.mjs';
import { appPublicKeys, keyList, parseKeyList } from './lib/app-key.mjs';
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

function trustedSigningKey(trusted) {
  const privateKey = readPrivateKey(keyFromEnvironment(PRIVATE_KEY_VARIABLE));
  if (!trusted.includes(rawPublicKey(privateKey))) {
    throw new Error(
      `${PRIVATE_KEY_VARIABLE} is not the pair of a key the app trusts (${keyList(trusted)})`,
    );
  }
  return privateKey;
}

try {
  const values = readOptions();
  const trusted = values['public-key'] ? parseKeyList(values['public-key']) : appPublicKeys();
  const privateKey = trustedSigningKey(trusted);
  const { fileName, feed } = readFeed(values.releases, values.channel);
  const published = feedOfVersion(feed, values.version);
  const signature = signFeed({ fileName, feed: published, privateKey });
  const publicKey = readPublicKey(rawPublicKey(privateKey));
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
