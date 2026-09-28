import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { describe, it } from 'node:test';
import { APP_PACKAGE, assetLinks, normaliseFingerprint } from '../lib/asset-links.mjs';

const FINGERPRINT =
  '14:6D:E9:83:C5:73:06:50:D8:EE:B9:95:2F:34:FC:64:16:A0:83:42:E6:1D:BE:A8:8A:04:96:B2:3F:CF:44:E5';

describe('normaliseFingerprint', () => {
  it('keeps the colon separated upper case form', () => {
    assert.equal(normaliseFingerprint(FINGERPRINT), FINGERPRINT);
  });

  it('accepts the bare lower case hexadecimal form', () => {
    assert.equal(normaliseFingerprint(FINGERPRINT.replaceAll(':', '').toLowerCase()), FINGERPRINT);
  });

  it('refuses anything but 32 bytes', () => {
    assert.throws(() => normaliseFingerprint('14:6D:E9'), /SHA-256/);
    assert.throws(() => normaliseFingerprint(`${FINGERPRINT}:00`), /SHA-256/);
    assert.throws(() => normaliseFingerprint('REPLACE_WITH_SIGNING_KEY_SHA256_FINGERPRINT'));
  });
});

describe('assetLinks', () => {
  it('lets the app handle the links of the site', () => {
    assert.deepEqual(assetLinks({ fingerprints: [FINGERPRINT] }), [
      {
        relation: ['delegate_permission/common.handle_all_urls'],
        target: {
          namespace: 'android_app',
          package_name: APP_PACKAGE,
          sha256_cert_fingerprints: [FINGERPRINT],
        },
      },
    ]);
  });

  it('lists each certificate once', () => {
    const [statement] = assetLinks({
      fingerprints: [FINGERPRINT, FINGERPRINT.toLowerCase(), 'AA'.repeat(32)],
    });

    assert.deepEqual(statement.target.sha256_cert_fingerprints, [
      FINGERPRINT,
      Array(32).fill('AA').join(':'),
    ]);
  });

  it('needs a fingerprint and a package name', () => {
    assert.throws(() => assetLinks({ fingerprints: [] }), /fingerprint/);
    assert.throws(() => assetLinks({ packageName: 'app', fingerprints: [FINGERPRINT] }), /package/);
  });

  it('names the package of the Android project', () => {
    const gradle = readFileSync(
      new URL('../../../../src/LoDb.Web/android/app/build.gradle', import.meta.url),
      'utf8',
    );
    const capacitor = readFileSync(
      new URL('../../../../src/LoDb.Web/capacitor.config.ts', import.meta.url),
      'utf8',
    );

    assert.match(gradle, new RegExp(`applicationId "${APP_PACKAGE.replaceAll('.', '\\.')}"`));
    assert.match(capacitor, new RegExp(`appId: '${APP_PACKAGE.replaceAll('.', '\\.')}'`));
  });
});
