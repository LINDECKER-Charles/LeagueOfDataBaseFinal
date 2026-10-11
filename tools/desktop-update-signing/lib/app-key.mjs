// The public key the app trusts, read from the source it is compiled from: the release checks
// a signature against exactly the key every installed copy holds, so a signing key that does
// not match it stops the release instead of reaching users who would refuse every update.
import { readFileSync } from 'node:fs';

export const APP_KEY_SOURCE = new URL(
  '../../../src/LoDb.Desktop/Updates/Engine/UpdateFeeds.cs',
  import.meta.url,
);
const DECLARATION = /\bFeedPublicKey\s*=\s*"([A-Za-z0-9+/]{43}=)"/g;

/** The base64 key of the FeedPublicKey constant; throws unless there is exactly one. */
export function appPublicKey(source = readFileSync(APP_KEY_SOURCE, 'utf8')) {
  const keys = [...source.matchAll(DECLARATION)].map((match) => match[1]);
  if (keys.length !== 1) {
    throw new Error(`expected one FeedPublicKey constant in UpdateFeeds.cs, found ${keys.length}`);
  }
  return keys[0];
}
