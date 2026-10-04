import { parseApkManifest } from './parse-apk-manifest';

const HOSTS = ['league-of-data-base.com', 'github.com'];
const MANIFEST = {
  versionCode: 2_004_000,
  versionName: '2.4.0',
  url: 'https://github.com/o/r/releases/download/android-v2.4.0/lodb-2.4.0.apk',
  sha256: 'b'.repeat(64),
};

describe('parseApkManifest', () => {
  it('reads a manifest whose every field holds', () => {
    expect(parseApkManifest(MANIFEST, HOSTS)).toEqual(MANIFEST);
  });

  it('drops the fields it does not know', () => {
    expect(parseApkManifest({ ...MANIFEST, notes: 'x' }, HOSTS)).toEqual(MANIFEST);
  });

  it.each([
    ['no object', null],
    ['a list', [MANIFEST]],
    ['a versionCode in a string', { ...MANIFEST, versionCode: '2004000' }],
    ['a fractional versionCode', { ...MANIFEST, versionCode: 2.5 }],
    ['a versionCode of zero', { ...MANIFEST, versionCode: 0 }],
    ['a versionCode above Play ceiling', { ...MANIFEST, versionCode: 2_100_000_001 }],
    ['a versionName with a v', { ...MANIFEST, versionName: 'v2.4.0' }],
    ['an http APK', { ...MANIFEST, url: 'http://github.com/o/r/lodb.apk' }],
    ['an APK from another host', { ...MANIFEST, url: 'https://evil.example/lodb.apk' }],
    [
      'an APK from a look-alike host',
      { ...MANIFEST, url: 'https://github.com.evil.example/a.apk' },
    ],
    ['a short SHA-256', { ...MANIFEST, sha256: 'b'.repeat(63) }],
    ['an upper case SHA-256', { ...MANIFEST, sha256: 'B'.repeat(64) }],
    ['no SHA-256', { ...MANIFEST, sha256: undefined }],
  ])('refuses %s', (_case, body) => {
    expect(parseApkManifest(body, HOSTS)).toBeNull();
  });
});
