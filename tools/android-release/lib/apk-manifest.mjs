// latest.json of the transitional channel (ADR 0008): the newest APK for the installations
// Play does not manage. The app (transitional/parse-apk-manifest.ts) offers it when its
// versionCode is above the installed one, then opens its URL in the system browser.
import { createHash } from 'node:crypto';
import { versionCodeOf } from './release-version.mjs';

// The app only opens APKs of the site or of the GitHub Releases (transitional-channel-token.ts).
const RELEASE_HOST = 'github.com';
const SHA256 = /^[0-9a-f]{64}$/;

/** SHA-256 of the APK in lower case hexadecimal. */
export function sha256Of(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

/** The manifest of the APK of `versionName`, checked with the rules of the app. */
export function apkManifest({ versionName, url, sha256 }) {
  const versionCode = versionCodeOf(versionName);
  const parsed = URL.parse(url);
  if (parsed?.protocol !== 'https:' || parsed.host !== RELEASE_HOST) {
    throw new Error(`the APK must be a release asset on https://${RELEASE_HOST}: ${url}`);
  }
  if (!SHA256.test(sha256)) {
    throw new Error(`not a SHA-256 in lower case hexadecimal: ${sha256}`);
  }
  return { versionCode, versionName, url, sha256 };
}

/** Throws unless the APK is the one the manifest describes. */
export function checkApk(manifest, bytes) {
  const actual = sha256Of(bytes);
  if (actual !== manifest.sha256) {
    throw new Error(`the APK has the SHA-256 ${actual}, latest.json says ${manifest.sha256}`);
  }
}
