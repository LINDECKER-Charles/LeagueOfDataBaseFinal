import { provideHttpClient } from '@angular/common/http';
import { type EnvironmentProviders, type Provider, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { API_BASE_URL } from '../../api/api-base-url';
import { ClientPolicyService } from '../../api/generated/services/client-policy.service';
import { AuthStrategies } from '../../auth/strategy/auth-strategies';
import { ActivePlatform } from '../detection/active-platform';
import type { UpdateState } from '../update-state';
import { AndroidPlatform } from './android-platform';
import { BearerAuthStrategy } from './auth/bearer-auth-strategy';
import { ANDROID_PLUGINS } from './native/android-plugins-token';
import { FakeAndroidPlugins } from './testing/fake-android-plugins';
import { AndroidUpdates } from './updates/android-updates';
import { UPDATE_PLUGINS } from './updates/native/update-plugins-token';
import { FakeAppUpdate } from './updates/testing/fake-app-update';
import { FakeLiveUpdate } from './updates/testing/fake-live-update';
import { TRANSITIONAL_CHANNEL } from './updates/transitional/transitional-channel-token';

// The updates as the platform sees them; their behaviour is android-updates.spec.ts's.
class FakeUpdates {
  readonly state = signal<UpdateState>('none');
  starts = 0;
  applies = 0;

  start(): void {
    this.starts++;
  }

  async apply(): Promise<void> {
    this.applies++;
  }
}

describe('AndroidPlatform', () => {
  let native: FakeAndroidPlugins;
  let updates: FakeUpdates;

  function start(afterDetection: (Provider | EnvironmentProviders)[] = []): AndroidPlatform {
    // No API_BASE_URL: the app derives it from PLATFORM, which the detection only sets once
    // this platform is built. Anything built here that reads it fails these specs.
    TestBed.configureTestingModule({
      providers: [
        { provide: ANDROID_PLUGINS, useValue: native.plugins },
        { provide: AndroidUpdates, useValue: updates },
        ...afterDetection,
      ],
    });
    return TestBed.inject(AndroidPlatform);
  }

  beforeEach(() => {
    native = new FakeAndroidPlugins();
    updates = new FakeUpdates();
  });

  it('is Android, authenticated by bearer tokens', () => {
    const platform = start();

    expect(platform.kind).toBe('android');
    expect(platform.authStrategy).toBe('bearer');
  });

  it('starts its updates as it is built, and relays their state', () => {
    const platform = start();

    expect(updates.starts).toBe(1);
    expect(platform.updateState()).toBe('none');

    updates.state.set('downloading');
    expect(platform.updateState()).toBe('downloading');
    updates.state.set('ready');
    expect(platform.updateState()).toBe('ready');
  });

  it('restarts on the update its updates hold', async () => {
    await start().applyUpdate();

    expect(updates.applies).toBe(1);
  });

  it('confirms the start of the bundle to the live update once the app has started', async () => {
    // The real updates, built without the API origin: the plugin rolls the bundle back
    // after its readyTimeout unless the app calls ready().
    const device = new FakeLiveUpdate();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: ANDROID_PLUGINS, useValue: native.plugins },
        {
          provide: UPDATE_PLUGINS,
          useValue: { liveUpdate: device.plugin, appUpdate: new FakeAppUpdate().plugin },
        },
        { provide: ActivePlatform, useValue: { detect: () => Promise.resolve({}) } },
        {
          provide: ClientPolicyService,
          useValue: { getClientPolicy: () => of({ platforms: [] }) },
        },
        { provide: TRANSITIONAL_CHANNEL, useValue: null },
      ],
    });
    TestBed.inject(AndroidPlatform);
    expect(device.readyCalls).toBe(0);

    await TestBed.inject(Router).navigateByUrl('/');

    await vi.waitFor(() => expect(device.readyCalls).toBe(1));
  });

  it('registers the bearer strategy with core/auth as it is built', () => {
    // Built on first use, once the detection has completed and the API origin is known.
    start([
      provideHttpClient(),
      provideRouter([]),
      { provide: API_BASE_URL, useValue: 'https://league-of-data-base.com' },
    ]);

    expect(TestBed.inject(AuthStrategies).get('bearer')).toBeInstanceOf(BearerAuthStrategy);
  });

  it('listens to the native events from the start', () => {
    start();

    expect([...native.listeners.keys()].sort()).toEqual(['appUrlOpen', 'backButton', 'resume']);
  });

  it('names the app and its native version once the plugin answered', async () => {
    native.version = '2.3.0';
    const platform = start();

    expect(platform.clientHeader()).toBeNull();
    await vi.waitFor(() => expect(platform.clientHeader()).toBe('android/2.3.0'));
  });

  it('keeps the page origin for the API when the build names no public one', () => {
    // The specs run on the web environment; the shell ones name the site's origin.
    expect(start().apiOrigin()).toBe(document.location.origin);
  });

  it('opens web links in the system browser', async () => {
    await start().openExternal('https://www.leagueoflegends.com/');

    expect(native.opened).toEqual(['https://www.leagueoflegends.com/']);
  });

  it.each(['javascript:alert(1)', 'intent://scan#Intent;end', 'file:///sdcard/x', 'nope'])(
    'refuses to open anything but a web link (%s)',
    async (url) => {
      await expect(start().openExternal(url)).rejects.toThrow(/http\(s\)/);
      expect(native.opened).toEqual([]);
    },
  );

  it('shares through the native sheet', async () => {
    const content = { title: 'Jinx', text: 'A build', url: 'https://league-of-data-base.com/b/1' };

    await expect(start().share(content)).resolves.toBe('shared');
    expect(native.shared).toEqual([content]);
  });

  it('tells a closed share sheet from a failure', async () => {
    const platform = start();

    native.shareAnswer = () => Promise.reject(new Error('Share canceled'));
    await expect(platform.share({ url: 'https://x.example/' })).resolves.toBe('dismissed');

    native.shareAnswer = () => Promise.reject(new Error('No activity found'));
    await expect(platform.share({ url: 'https://x.example/' })).rejects.toThrow('No activity');
  });

  it('refuses to save a file, which the WebView cannot download', async () => {
    const file = new File(['{}'], 'build.json', { type: 'application/json' });

    await expect(start().saveFile(file)).rejects.toThrow(/not available/);
  });
});
