// Generates an Ed25519 key pair for the desktop release feeds, on the owner's machine:
//
//   node tools/desktop-update-signing/generate-key.mjs --out <secure folder>/feed-signing.pem
//
// Writes the private key (PKCS#8 PEM, readable by its owner only) to --out, which must not
// exist yet nor lie inside a git work tree, and prints the public key only: it goes into
// UpdateFeeds.FeedPublicKeys. The private key becomes the release secret
// (`gh secret set LODB_DESKTOP_UPDATE_PRIVATE_KEY < <file>`) and its offline backups.
import { generateKeyPairSync } from 'node:crypto';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { parseArgs } from 'node:util';
import { rawPublicKey } from './lib/feed-signature.mjs';

const OWNER_ONLY_FILE = 0o600;
const OWNER_ONLY_FOLDER = 0o700;

// A key written inside a checkout is one `git add` away from the history, for good.
function enclosingRepository(folder) {
  for (let current = folder; ; current = dirname(current)) {
    if (existsSync(join(current, '.git'))) {
      return current;
    }
    if (dirname(current) === current) {
      return null;
    }
  }
}

try {
  const { values } = parseArgs({ options: { out: { type: 'string' } } });
  if (!values.out) {
    throw new Error('--out is required: a file outside any repository');
  }
  const out = resolve(values.out);
  const repository = enclosingRepository(dirname(out));
  if (repository) {
    throw new Error(`${out} lies inside the git work tree ${repository}`);
  }
  const { privateKey, publicKey } = generateKeyPairSync('ed25519');
  mkdirSync(dirname(out), { recursive: true, mode: OWNER_ONLY_FOLDER });
  // wx: never overwrite a key, which would lose the one releases are signed with.
  writeFileSync(out, privateKey.export({ type: 'pkcs8', format: 'pem' }), {
    flag: 'wx',
    mode: OWNER_ONLY_FILE,
  });
  console.error(`generate-key: private key written to ${out}`);
  process.stdout.write(`${rawPublicKey(publicKey)}\n`);
} catch (error) {
  console.error(`generate-key: ${error.message}`);
  process.exitCode = 1;
}
