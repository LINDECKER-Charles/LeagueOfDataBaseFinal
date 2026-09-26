import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ANDROID_PLUGINS } from '../../native/android-plugins-token';
import { FakeAndroidPlugins } from '../../testing/fake-android-plugins';
import type { TransitionalSettings } from './transitional-settings';
import { TransitionalChannel } from './transitional-channel';
import { TRANSITIONAL_CHANNEL } from './transitional-channel-token';

const SETTINGS: TransitionalSettings = {
  manifestUrl: 'https://league-of-data-base.com/android/latest.json',
  apkHosts: ['league-of-data-base.com', 'github.com'],
};
const MANIFEST = {
  versionCode: 2_004_000,
  versionName: '2.4.0',
  url: 'https://github.com/o/r/releases/download/android-v2.4.0/lodb-2.4.0.apk',
  sha256: 'b'.repeat(64),
};

describe('TransitionalChannel', () => {
  let native: FakeAndroidPlugins;
  let intercepted: number;

  function channel(settings: TransitionalSettings | null = SETTINGS): TransitionalChannel {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([
            (request, next) => {
              intercepted++;
              return next(request);
            },
          ]),
        ),
        provideHttpClientTesting(),
        { provide: ANDROID_PLUGINS, useValue: native.plugins },
        { provide: TRANSITIONAL_CHANNEL, useValue: settings },
      ],
    });
    return TestBed.inject(TransitionalChannel);
  }

  async function answer(body: object, running: Promise<void>): Promise<void> {
    await vi.waitFor(() =>
      TestBed.inject(HttpTestingController).expectOne(SETTINGS.manifestUrl).flush(body),
    );
    await running;
  }

  beforeEach(() => {
    native = new FakeAndroidPlugins();
    intercepted = 0;
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('offers a newer APK and opens it in the system browser', async () => {
    const service = channel();

    await answer(MANIFEST, service.run(2_003_000));
    expect(service.state()).toBe('ready');

    await service.apply();
    expect(native.opened).toEqual([MANIFEST.url]);
  });

  it('reads the manifest without the API interceptors', async () => {
    const service = channel();

    await answer(MANIFEST, service.run(2_003_000));

    expect(intercepted).toBe(0);
  });

  it.each([2_004_000, 2_005_000])('offers nothing to an app at versionCode %i', async (code) => {
    const service = channel();

    await answer(MANIFEST, service.run(code));

    expect(service.state()).toBe('none');
    await service.apply();
    expect(native.opened).toEqual([]);
  });

  it('refuses an APK from a host it does not trust', async () => {
    const service = channel();

    await answer({ ...MANIFEST, url: 'https://evil.example/lodb.apk' }, service.run(1));

    expect(service.state()).toBe('none');
  });

  it('keeps its offer when the manifest cannot be read', async () => {
    const service = channel();
    await answer(MANIFEST, service.run(2_003_000));

    const running = service.run(2_003_000);
    await vi.waitFor(() =>
      TestBed.inject(HttpTestingController)
        .expectOne(SETTINGS.manifestUrl)
        .flush(null, { status: 404, statusText: 'Not Found' }),
    );
    await running;

    expect(service.state()).toBe('ready');
  });

  it('reads nothing when the channel is off', async () => {
    const service = channel(null);

    await service.run(1);

    expect(service.state()).toBe('none');
  });
});
