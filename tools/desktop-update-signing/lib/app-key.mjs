// The public keys the app trusts, read from the source it is compiled from: the release checks
// a signature against exactly the keys every installed copy holds, so a signing key that is
// not among them stops the release instead of reaching users who would refuse every update.
import { readFileSync } from 'node:fs';

export const APP_KEY_SOURCE = new URL(
  '../../../src/LoDb.Desktop/Updates/Engine/UpdateFeeds.cs',
  import.meta.url,
);
const LIST = /\bFeedPublicKeys\s*=\s*\[([^\]]*)\]/g;
const KEY = /"([A-Za-z0-9+/]{43}=)"/g;
const ANY_STRING = /"[^"]*"/g;

/** The base64 keys of the FeedPublicKeys list; throws unless it is one list of valid keys. */
export function appPublicKeys(source = readFileSync(APP_KEY_SOURCE, 'utf8')) {
  const lists = [...source.matchAll(LIST)].map((match) => match[1]);
  if (lists.length !== 1) {
    throw new Error(`expected one FeedPublicKeys list in UpdateFeeds.cs, found ${lists.length}`);
  }
  const keys = [...lists[0].matchAll(KEY)].map((match) => match[1]);
  if (keys.length === 0 || keys.length !== lists[0].match(ANY_STRING)?.length) {
    throw new Error('FeedPublicKeys must hold Ed25519 keys in base64 only');
  }
  return keys;
}

/** The keys as one value, the form of the smoke report and of --public-key. */
export function keyList(keys) {
  return keys.join(' ');
}

/** The keys of a --public-key value: base64 keys separated by spaces. */
export function parseKeyList(value) {
  return String(value).trim().split(/\s+/).filter(Boolean);
}
