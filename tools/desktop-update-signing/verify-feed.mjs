// Checks the feed of a release folder as the app will, before and after its upload:
//
//   node tools/desktop-update-signing/verify-feed.mjs --releases DIR --channel win-x64 \
//        [--public-key BASE64] [--packages]
//
// The signature of releases.<channel>.json must hold against the key the app trusts (the one
// compiled into it, or --public-key for a local E2E build), and every entry must carry the
// SHA-256 the app requires. --packages also checks the size and SHA-256 of each listed package
// against the folder's files: what is uploaded must be what the signed feed names.
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseArgs } from 'node:util';
import { appPublicKey } from './lib/app-key.mjs';
import { checkPackages } from './lib/feed-packages.mjs';
import { readPublicKey, signatureHolds } from './lib/feed-signature.mjs';
import { checkFeed, readFeed } from './lib/release-feed.mjs';

function readOptions() {
  const { values } = parseArgs({
    options: {
      releases: { type: 'string' },
      channel: { type: 'string' },
      'public-key': { type: 'string' },
      packages: { type: 'boolean', default: false },
    },
  });
  if (!values.releases || !values.channel) {
    throw new Error('--releases and --channel are required');
  }
  return values;
}

function readSignature(directory, fileName) {
  try {
    return readFileSync(join(directory, `${fileName}.sig`), 'utf8');
  } catch {
    throw new Error(`${fileName} has no signature (${fileName}.sig)`);
  }
}

try {
  const values = readOptions();
  const publicKey = readPublicKey(values['public-key'] ?? appPublicKey());
  const { fileName, feed } = readFeed(values.releases, values.channel);
  const signature = readSignature(values.releases, fileName);
  if (!signatureHolds({ fileName, feed, signature, publicKey })) {
    throw new Error(`the signature of ${fileName} does not hold`);
  }
  const { Assets: assets } = checkFeed(feed);
  if (values.packages) {
    await checkPackages({ assets, directory: values.releases });
  }
  const checked = values.packages ? `, ${assets.length} package(s) match` : '';
  console.error(`verify-feed: ${fileName} holds${checked} (${values.releases})`);
} catch (error) {
  console.error(`verify-feed: ${error.message}`);
  process.exitCode = 1;
}
