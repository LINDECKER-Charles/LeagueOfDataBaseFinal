import type { LiveUpdateBundle } from '../../../../api/generated/models/live-update-bundle';
import type { PlatformPolicy } from '../../../../api/generated/models/platform-policy';
import { planUpdate } from './plan-update';
import type { UpdateFacts } from './update-facts';

const BUNDLE: LiveUpdateBundle = {
  id: '2.4.0',
  url: 'https://github.com/o/r/releases/download/android-v2.4.0/lodb-bundle-2.4.0.zip',
  checksum: 'a'.repeat(64),
  signature: 'c2lnbmF0dXJl',
  minimumNativeVersion: '2.0.0',
};

function policy(overrides: Partial<PlatformPolicy> = {}): PlatformPolicy {
  return { platform: 'android', minimumVersion: '1.0.0', latestVersion: '2.3.0', ...overrides };
}

function facts(overrides: Partial<UpdateFacts> = {}): UpdateFacts {
  return {
    nativeVersion: '2.3.0',
    policy: policy({ bundle: BUNDLE }),
    nextBundleId: null,
    downloadedBundleIds: [],
    rejectedBundleIds: [],
    ...overrides,
  };
}

describe('planUpdate', () => {
  describe('native shell', () => {
    it('updates nothing natively when the shell is the latest', () => {
      expect(planUpdate(facts()).native).toBe('none');
    });

    it('offers a flexible update below the latest version', () => {
      expect(planUpdate(facts({ policy: policy({ latestVersion: '2.4.0' }) })).native).toBe(
        'flexible',
      );
    });

    it('forces an immediate update below the minimum, and no bundle then', () => {
      const plan = planUpdate(
        facts({
          policy: policy({ minimumVersion: '2.4.0', latestVersion: '2.4.0', bundle: BUNDLE }),
        }),
      );

      expect(plan).toEqual({ native: 'immediate', live: { kind: 'keep' } });
    });

    it('treats a beta of the minimum as below it', () => {
      const plan = planUpdate(
        facts({ nativeVersion: '2.4.0-beta.1', policy: policy({ minimumVersion: '2.4.0' }) }),
      );

      expect(plan.native).toBe('immediate');
    });

    it('decides nothing natively on versions it cannot read', () => {
      expect(planUpdate(facts({ nativeVersion: 'dev' })).native).toBe('none');
      expect(
        planUpdate(facts({ policy: policy({ minimumVersion: null, latestVersion: null }) })).native,
      ).toBe('none');
    });
  });

  describe('live bundle', () => {
    it('downloads the bundle of the policy', () => {
      expect(planUpdate(facts()).live).toEqual({ kind: 'download', bundle: BUNDLE });
    });

    it('starts on a bundle already on the device without downloading it again', () => {
      expect(planUpdate(facts({ downloadedBundleIds: ['2.4.0'] })).live).toEqual({
        kind: 'activate',
        bundleId: '2.4.0',
      });
    });

    it('keeps a start already set on the bundle of the policy', () => {
      expect(
        planUpdate(facts({ nextBundleId: '2.4.0', downloadedBundleIds: ['2.4.0'] })).live,
      ).toEqual({
        kind: 'keep',
      });
    });

    it('never takes a bundle that needs a newer shell', () => {
      const live = planUpdate(
        facts({
          nativeVersion: '1.9.0',
          policy: policy({ latestVersion: '2.3.0', bundle: BUNDLE }),
        }),
      ).live;

      expect(live).toEqual({ kind: 'keep' });
    });

    it('never takes again a bundle that failed to start on this device', () => {
      expect(planUpdate(facts({ rejectedBundleIds: ['2.4.0'] })).live).toEqual({ kind: 'keep' });
    });

    it('goes back to the embedded bundle when the policy withdraws its bundle', () => {
      const live = planUpdate(
        facts({ policy: policy({ bundle: null }), nextBundleId: '2.4.0' }),
      ).live;

      expect(live).toEqual({ kind: 'reset' });
    });

    it('goes back to the embedded bundle when the one it runs needs a newer shell', () => {
      const withdrawn = facts({
        nativeVersion: '1.9.0',
        policy: policy({ bundle: { ...BUNDLE, id: '2.5.0', minimumNativeVersion: '2.0.0' } }),
        nextBundleId: '2.4.0',
      });

      expect(planUpdate(withdrawn).live).toEqual({ kind: 'reset' });
    });

    it('moves from a withdrawn bundle to the one that replaces it', () => {
      const replaced = facts({
        policy: policy({ bundle: { ...BUNDLE, id: '2.4.1' } }),
        nextBundleId: '2.4.0',
        downloadedBundleIds: ['2.4.0'],
        rejectedBundleIds: ['2.4.0'],
      });

      expect(planUpdate(replaced).live).toEqual({
        kind: 'download',
        bundle: { ...BUNDLE, id: '2.4.1' },
      });
    });

    it.each([
      ['an http URL', { url: 'http://example.com/b.zip' }],
      ['a checksum in upper case', { checksum: 'A'.repeat(64) }],
      ['a short checksum', { checksum: 'a'.repeat(63) }],
      ['a signature with blanks', { signature: 'c2ln bmF0' }],
      ['the id reserved by the plugin', { id: 'public' }],
      ['an id with a slash', { id: '../2.4.0' }],
      ['a pre-release as minimum shell', { minimumNativeVersion: '2.0.0-beta.1' }],
      ['a missing field', { signature: undefined }],
    ])('treats a bundle with %s as no bundle', (_case, fields) => {
      const malformed = { ...BUNDLE, ...fields } as LiveUpdateBundle;

      expect(planUpdate(facts({ policy: policy({ bundle: malformed }) })).live).toEqual({
        kind: 'keep',
      });
      expect(
        planUpdate(facts({ policy: policy({ bundle: malformed }), nextBundleId: '2.3.0' })).live,
      ).toEqual({ kind: 'reset' });
    });
  });
});
