import { TestBed } from '@angular/core/testing';
import type { LiveUpdateBundle } from '../../../../api/generated/models/live-update-bundle';
import { ANDROID_PLUGINS } from '../../native/android-plugins-token';
import { FakeAndroidPlugins } from '../../testing/fake-android-plugins';
import { UPDATE_PLUGINS } from '../native/update-plugins-token';
import { FakeAppUpdate } from '../testing/fake-app-update';
import { FakeLiveUpdate } from '../testing/fake-live-update';
import { LiveUpdates } from './live-updates';
import { RejectedBundles } from './rejected-bundles';

const BUNDLE: LiveUpdateBundle = {
  id: '2.4.0',
  url: 'https://github.com/o/r/releases/download/android-v2.4.0/lodb-bundle-2.4.0.zip',
  checksum: 'a'.repeat(64),
  signature: 'c2lnbmF0dXJl',
  minimumNativeVersion: '2.0.0',
};

describe('LiveUpdates', () => {
  let native: FakeAndroidPlugins;
  let device: FakeLiveUpdate;

  // A new process on the same device: the plugin and the secure storage outlive it.
  function launch(): LiveUpdates {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: ANDROID_PLUGINS, useValue: native.plugins },
        {
          provide: UPDATE_PLUGINS,
          useValue: { liveUpdate: device.plugin, appUpdate: new FakeAppUpdate().plugin },
        },
      ],
    });
    return TestBed.inject(LiveUpdates);
  }

  beforeEach(() => {
    native = new FakeAndroidPlugins();
    device = new FakeLiveUpdate();
  });

  it('downloads a bundle with its checksum and signature, for the next start', async () => {
    const live = launch();
    await live.confirmStart();

    await live.run({ kind: 'download', bundle: BUNDLE });

    expect(device.downloads).toEqual([
      {
        bundleId: '2.4.0',
        url: BUNDLE.url,
        checksum: BUNDLE.checksum,
        signature: BUNDLE.signature,
      },
    ]);
    expect(device.next).toBe('2.4.0');
    expect(device.current).toBeNull();
    expect(live.state()).toBe('ready');
  });

  it('says it downloads while the plugin downloads', async () => {
    const live = launch();
    let release!: () => void;
    device.downloadDelay = () =>
      new Promise<void>((resolve) => {
        release = resolve;
      });
    const running = live.run({ kind: 'download', bundle: BUNDLE });

    expect(live.state()).toBe('downloading');
    release();
    await running;
    expect(live.state()).toBe('ready');
  });

  it('keeps the bundles it had when a signature does not hold', async () => {
    device.signatureHolds = () => false;
    const live = launch();

    await live.run({ kind: 'download', bundle: BUNDLE });

    expect(device.downloaded.size).toBe(0);
    expect(device.next).toBeNull();
    expect(live.state()).toBe('none');
  });

  it('starts on a bundle already downloaded, or back on the embedded one', async () => {
    device.downloaded.add('2.4.0');
    const live = launch();

    await live.run({ kind: 'activate', bundleId: '2.4.0' });
    expect(device.next).toBe('2.4.0');

    await live.run({ kind: 'reset' });
    expect(device.next).toBeNull();
    expect(live.state()).toBe('none');
  });

  it('restarts on the next bundle when asked', async () => {
    const live = launch();
    await live.run({ kind: 'download', bundle: BUNDLE });

    await live.apply();

    expect(device.reloads).toBe(1);
    expect(device.current).toBe('2.4.0');
  });

  describe('faulty bundle', () => {
    async function startOnFaultyBundle(): Promise<void> {
      const first = launch();
      await first.confirmStart();
      await first.run({ kind: 'download', bundle: BUNDLE });
      // Cold start on the bundle, which crashes before it can confirm its start.
      device.coldStart();
      device.expireReadyTimeout();
    }

    it('is rolled back by the plugin when its start is never confirmed', async () => {
      await startOnFaultyBundle();

      expect(device.current).toBeNull();
      expect(device.next).toBeNull();
    });

    it('is remembered and removed by the embedded bundle that starts instead', async () => {
      await startOnFaultyBundle();

      const embedded = launch();
      await embedded.confirmStart();

      expect(await TestBed.inject(RejectedBundles).list()).toEqual(['2.4.0']);
      expect(device.downloaded.has('2.4.0')).toBe(false);
      expect((await embedded.facts()).rejectedBundleIds).toEqual(['2.4.0']);
      expect(embedded.state()).toBe('none');
    });

    it('is not held against a bundle that confirmed its start', async () => {
      const first = launch();
      await first.confirmStart();
      await first.run({ kind: 'download', bundle: BUNDLE });
      device.coldStart();

      await launch().confirmStart();
      device.expireReadyTimeout();

      expect(device.current).toBe('2.4.0');
      expect(await TestBed.inject(RejectedBundles).list()).toEqual([]);
    });
  });
});
