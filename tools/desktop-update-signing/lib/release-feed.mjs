// The Velopack feed of a release (releases.<channel>.json): what the app reads to find a newer
// version and the SHA-256 of each package, which Velopack checks after the download. Once the
// feed is signed, these checksums are what authenticates the packages: an entry without one
// would fall back to SHA-1, so the app refuses it, and so does this tool.
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

// Velopack channels of the release workflow: <rid> or <rid>-beta (lib.sh).
const CHANNEL = /^[a-z0-9]+(-[a-z0-9]+)+$/;
const PACKAGE_NAME = /^[A-Za-z0-9._-]+\.nupkg$/;
const SHA256 = /^[0-9A-Fa-f]{64}$/;

// The keys vpk writes (VelopackAssetFeed and VelopackAsset), under their exact names. The app
// matches key names without case and keeps the last of two that differ only by case, where
// these checks read the exact name: a feed holding any other key could name one package to them
// and another to the app, so it is neither signed nor verified.
const FEED_KEYS = new Set(['Assets']);
const ENTRY_KEYS = new Set([
  'PackageId',
  'Version',
  'Type',
  'FileName',
  'SHA1',
  'SHA256',
  'Size',
  'NotesMarkdown',
  'NotesHTML',
]);

/** The feed's file name for a Velopack channel, as Velopack names it. */
export function feedFileName(channel) {
  if (!CHANNEL.test(channel)) {
    throw new Error(`invalid Velopack channel: ${JSON.stringify(channel)}`);
  }
  return `releases.${channel}.json`;
}

/** Reads the feed of the channel from a release folder: its name and its bytes. */
export function readFeed(directory, channel) {
  const fileName = feedFileName(channel);
  return { fileName, feed: readFileSync(join(directory, fileName)) };
}

/** Throws on the first entry the app would refuse; returns the parsed feed. */
export function checkFeed(feed) {
  const parsed = JSON.parse(feed.toString('utf8'));
  if (!Array.isArray(parsed?.Assets) || parsed.Assets.length === 0) {
    throw new Error('the feed lists no package');
  }
  checkKeys(parsed, FEED_KEYS, 'the feed');
  parsed.Assets.forEach(checkAsset);
  return parsed;
}

/**
 * The feed of one release, as published with it: vpk pack lists every package of the
 * release folder (N-1 fetched for the delta included), while the GitHub Release of version V
 * only holds the packages of V.
 */
export function feedOfVersion(feed, version) {
  const own = checkFeed(feed).Assets.filter((asset) => asset.Version === version);
  if (own.length === 0) {
    throw new Error(`the feed lists no package of version ${version}`);
  }
  return Buffer.from(JSON.stringify({ Assets: own }), 'utf8');
}

function checkKeys(object, known, owner) {
  const unexpected = Object.keys(object).filter((key) => !known.has(key));
  if (unexpected.length > 0) {
    const keys = unexpected.map((key) => JSON.stringify(key)).join(', ');
    throw new Error(`${owner} holds unexpected keys: ${keys}`);
  }
}

function checkAsset(asset) {
  const name = asset?.FileName;
  if (typeof name !== 'string' || !PACKAGE_NAME.test(name)) {
    throw new Error(`invalid package name in the feed: ${JSON.stringify(name)}`);
  }
  checkKeys(asset, ENTRY_KEYS, name);
  if (typeof asset.SHA256 !== 'string' || !SHA256.test(asset.SHA256)) {
    throw new Error(`${name} has no SHA-256 in the feed`);
  }
  if (!Number.isSafeInteger(asset.Size) || asset.Size <= 0) {
    throw new Error(`${name} has no size in the feed`);
  }
  if (typeof asset.Version !== 'string' || asset.Version.length === 0) {
    throw new Error(`${name} has no version in the feed`);
  }
}
