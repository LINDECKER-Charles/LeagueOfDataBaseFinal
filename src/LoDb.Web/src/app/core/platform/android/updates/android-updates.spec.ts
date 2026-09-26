import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { FlexibleUpdateInstallStatus } from '@capawesome/capacitor-app-update';
import { defer, of, throwError } from 'rxjs';
import type { LiveUpdateBundle } from '../../../api/generated/models/live-update-bundle';
import type { PlatformPolicy } from '../../../api/generated/models/platform-policy';
import { ClientPolicyService } from '../../../api/generated/services/client-policy.service';
import { ClientUpdate } from '../../../update/client-update';
import { ActivePlatform } from '../../detection/active-platform';
import { AndroidLifecycle } from '../lifecycle/android-lifecycle';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';
import { FakeAndroidPlugins } from '../testing/fake-android-plugins';
import { AndroidUpdates } from './android-updates';
import { CHECK_INTERVAL_MS } from './check/check-schedule';
import { UPDATE_PLUGINS } from './native/update-plugins-token';
import { FakeAppUpdate } from './testing/fake-app-update';
import { FakeLiveUpdate } from './testing/fake-live-update';
import { TRANSITIONAL_CHANNEL } from './transitional/transitional-channel-token';

const MANIFEST_URL = 'https://league-of-data-base.com/android/latest.json';
const APK_URL = 'https://github.com/o/r/releases/download/android-v2.4.0/lodb-2.4.0.apk';

function bundle(id: string): LiveUpdateBundle {
  return {
    id,
    url: `https://github.com/o/r/releases/download/android-v${id}/lodb-bundle-${id}.zip`,
    checksum: 'a'.repeat(64),
    signature: 'c2lnbmF0dXJl',
    minimumNativeVersion: '2.0.0',
  };
}

describe('AndroidUpdates', () => {
  let native: FakeAndroidPlugins;
  let device: FakeLiveUpdate;
  let play: FakeAppUpdate;
  let policy: PlatformPolicy;
  let policyReads: number;
  let policyFails: boolean;

  // A new process of the app on the same device: the plugins' state and the secure storage
  // outlive it, the listeners of the previous process do not.
  function launch(): AndroidUpdates {
    const previous = native;
    native = new FakeAndroidPlugins();
    previous.stored.forEach((value, key) => native.stored.set(key, value));
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ActivePlatform, useValue: { detect: () => Promise.resolve({}) } },
        { provide: ANDROID_PLUGINS, useValue: native.plugins },
        {
          provide: UPDATE_PLUGINS,
          useValue: { liveUpdate: device.plugin, appUpdate: play.plugin },
        },
        { provide: ClientPolicyService, useValue: { getClientPolicy: () => readPolicy() } },
        {
          provide: TRANSITIONAL_CHANNEL,
          useValue: { manifestUrl: MANIFEST_URL, apkHosts: ['github.com'] },
        },
      ],
    });
    // The platform starts both at detection (L10.2).
    TestBed.inject(AndroidLifecycle).start();
    const updates = TestBed.inject(AndroidUpdates);
    updates.start();
    return updates;
  }

  function readPolicy() {
    return defer(() => {
      policyReads++;
      const desktop = { platform: 'desktop' as const, minimumVersion: '9.0.0' };
      return policyFails
        ? throwError(() => new Error('offline'))
        : of({ platforms: [desktop, policy] });
    });
  }

  async function launched(): Promise<AndroidUpdates> {
    const reads = policyReads;
    const updates = launch();
    await TestBed.inject(Router).navigateByUrl('/');
    await checked(reads + 1);
    return updates;
  }

  // Every fake answers at once: past the read of the policy, one macrotask ends the check.
  async function checked(reads: number): Promise<void> {
    await vi.waitFor(() => expect(policyReads).toBe(reads));
    await new Promise((resolve) => setTimeout(resolve));
  }

  function returnToForeground(afterMs: number): void {
    vi.setSystemTime(Date.now() + afterMs);
    native.emit('resume');
  }

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    native = new FakeAndroidPlugins();
    device = new FakeLiveUpdate();
    play = new FakeAppUpdate();
    policy = { platform: 'android', minimumVersion: '1.0.0', latestVersion: '2.3.0', bundle: null };
    policyReads = 0;
    policyFails = false;
  });

  afterEach(() => vi.useRealTimers());

  it('confirms the start of the bundle once the app has started, then checks', async () => {
    launch();
    await new Promise((resolve) => setTimeout(resolve));
    expect(device.readyCalls).toBe(0);
    expect(policyReads).toBe(0);

    await TestBed.inject(Router).navigateByUrl('/');

    await checked(1);
    expect(device.readyCalls).toBe(1);
  });

  it('prepares the bundle of the policy for the next start and announces it', async () => {
    policy.bundle = bundle('2.4.0');

    const updates = await launched();

    expect(device.next).toBe('2.4.0');
    expect(device.current).toBeNull();
    expect(updates.state()).toBe('ready');
  });

  it('rolls a faulty bundle back, never takes it again, then takes its replacement', async () => {
    policy.bundle = bundle('2.4.0');
    await launched();
    // Cold start on the faulty bundle: it never starts, the plugin rolls it back.
    device.coldStart();
    device.expireReadyTimeout();

    let updates = await launched();

    expect(device.current).toBeNull();
    expect(device.next).toBeNull();
    expect(device.downloads.map(({ bundleId }) => bundleId)).toEqual(['2.4.0']);
    expect(device.downloaded.has('2.4.0')).toBe(false);
    expect(updates.state()).toBe('none');

    // The API withdraws it: the policy names a sound bundle.
    policy.bundle = bundle('2.4.1');
    returnToForeground(CHECK_INTERVAL_MS);
    await checked(3);
    expect(device.next).toBe('2.4.1');
    expect(updates.state()).toBe('ready');

    device.coldStart();
    updates = await launched();
    device.expireReadyTimeout();

    expect(device.current).toBe('2.4.1');
    expect(updates.state()).toBe('none');
  });

  it('goes back to the embedded bundle when the policy withdraws the running one', async () => {
    policy.bundle = bundle('2.4.0');
    await launched();
    device.coldStart();
    const updates = await launched();
    expect(device.current).toBe('2.4.0');

    policy.bundle = null;
    returnToForeground(CHECK_INTERVAL_MS);
    await checked(3);

    expect(device.next).toBeNull();
    expect(updates.state()).toBe('ready');
  });

  it('changes nothing when the policy cannot be read', async () => {
    device.downloaded.add('2.4.0');
    device.next = '2.4.0';
    device.coldStart();
    policyFails = true;

    const updates = await launched();

    expect(device.next).toBe('2.4.0');
    expect(device.downloads).toEqual([]);
    expect(updates.state()).toBe('none');
  });

  it('checks again on a return to the foreground, at most every 15 minutes', async () => {
    await launched();

    // The fake clock keeps running: the exact bound is the schedule's own spec.
    returnToForeground(CHECK_INTERVAL_MS / 2);
    await new Promise((resolve) => setTimeout(resolve));
    expect(policyReads).toBe(1);

    returnToForeground(CHECK_INTERVAL_MS / 2);
    await checked(2);
  });

  describe('below the minimum version (426)', () => {
    it('updates the shell through Play at once when the API refuses the app', async () => {
      await launched();
      policy = {
        ...policy,
        minimumVersion: '2.4.0',
        latestVersion: '2.4.0',
        bundle: bundle('2.4.0'),
      };
      play.offer('2004000');

      TestBed.inject(ClientUpdate).require({
        clientVersion: '2.3.0',
        minimumVersion: '2.4.0',
        latestVersion: '2.4.0',
      });
      TestBed.tick();

      await checked(2);
      expect(play.immediateUpdates).toBe(1);
      expect(device.downloads).toEqual([]);
    });

    it('offers the APK of the transitional channel outside Play', async () => {
      play.unmanaged = true;
      policy = { ...policy, minimumVersion: '2.4.0', latestVersion: '2.4.0' };
      const updates = launch();
      await TestBed.inject(Router).navigateByUrl('/');

      await vi.waitFor(() =>
        TestBed.inject(HttpTestingController)
          .expectOne(MANIFEST_URL)
          .flush({
            versionCode: 2_004_000,
            versionName: '2.4.0',
            url: APK_URL,
            sha256: 'b'.repeat(64),
          }),
      );
      await vi.waitFor(() => expect(updates.state()).toBe('ready'));

      await updates.apply();
      expect(native.opened).toEqual([APK_URL]);
    });
  });

  it('restarts on the native update first, which brings its own front', async () => {
    policy = { ...policy, latestVersion: '2.4.0', bundle: bundle('2.4.0') };
    play.offer('2004000');
    const updates = await launched();
    play.emit({
      installStatus: FlexibleUpdateInstallStatus.DOWNLOADED,
      bytesDownloaded: undefined,
      totalBytesToDownload: undefined,
    });
    expect(updates.state()).toBe('ready');

    await updates.apply();

    expect(play.completions).toBe(1);
    expect(device.reloads).toBe(0);
  });

  it('restarts the page on a ready bundle', async () => {
    policy.bundle = bundle('2.4.0');
    const updates = await launched();

    await updates.apply();

    expect(device.reloads).toBe(1);
    expect(device.current).toBe('2.4.0');
  });
});
