// Digital Asset Links statement of the Android app (ADR 0007, ADR 0009): the site vouches
// for the certificates allowed to open its App Links, so that Android verifies the OAuth
// return without asking the user which app should open it.

/** Package of the app, as capacitor.config.ts and android/app/build.gradle declare it. */
export const APP_PACKAGE = 'com.leagueofdatabase.app';

const PACKAGE_NAME = /^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$/;
// What keytool and the Play Console print: 32 bytes in hexadecimal, colon separated.
const FINGERPRINT = /^[0-9A-F]{2}(:[0-9A-F]{2}){31}$/;

/**
 * Normalises a SHA-256 certificate fingerprint to the form assetlinks.json requires
 * (upper case, colon separated). Accepts it with or without colons, in either case.
 */
export function normaliseFingerprint(value) {
  const hex = String(value).trim().replaceAll(':', '').toUpperCase();
  const fingerprint = hex.match(/.{1,2}/g)?.join(':') ?? '';
  if (!FINGERPRINT.test(fingerprint)) {
    throw new Error(`not a SHA-256 certificate fingerprint: ${value}`);
  }
  return fingerprint;
}

/**
 * The statement list served at /.well-known/assetlinks.json. Several fingerprints can be
 * listed: Play's app signing key, the upload key, a debug key during a test.
 */
export function assetLinks({ packageName = APP_PACKAGE, fingerprints }) {
  if (!PACKAGE_NAME.test(packageName)) {
    throw new Error(`not an Android package name: ${packageName}`);
  }
  if (!fingerprints?.length) {
    throw new Error('at least one certificate fingerprint is required');
  }
  const unique = [...new Set(fingerprints.map(normaliseFingerprint))];
  return [
    {
      relation: ['delegate_permission/common.handle_all_urls'],
      target: {
        namespace: 'android_app',
        package_name: packageName,
        sha256_cert_fingerprints: unique,
      },
    },
  ];
}
