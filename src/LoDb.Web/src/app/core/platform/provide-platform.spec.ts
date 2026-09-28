import { ApplicationInitStatus, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivePlatform } from './detection/active-platform';
import { PLATFORM } from './platform';
import type { PlatformService } from './platform-service';
import { providePlatform } from './provide-platform';
import { WebPlatform } from './web/web-platform';

describe('providePlatform', () => {
  const globals = window as unknown as Record<string, unknown>;

  afterEach(() => {
    delete globals['__LODB_DESKTOP__'];
    delete globals['Capacitor'];
  });

  async function startedPlatform(platformId = 'browser'): Promise<PlatformService> {
    TestBed.configureTestingModule({
      providers: [providePlatform(), { provide: PLATFORM_ID, useValue: platformId }],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    return TestBed.inject(PLATFORM);
  }

  it('detects the web in a plain browser', async () => {
    const platform = await startedPlatform();

    expect(platform).toBeInstanceOf(WebPlatform);
    expect(platform.kind).toBe('web');
  });

  it('loads the desktop implementation when the Photino host marked the page', async () => {
    globals['__LODB_DESKTOP__'] = { version: '1.4.0' };

    expect((await startedPlatform()).kind).toBe('desktop');
  });

  it('loads the Android implementation inside the Capacitor WebView', async () => {
    globals['Capacitor'] = { getPlatform: () => 'android' };

    expect((await startedPlatform()).kind).toBe('android');
  });

  it('stays on the web while rendering on the server, whatever the globals say', async () => {
    globals['__LODB_DESKTOP__'] = { version: '1.4.0' };

    expect((await startedPlatform('server')).kind).toBe('web');
  });

  it('detects once, however many callers await the detection', async () => {
    const platform = await startedPlatform();

    await expect(TestBed.inject(ActivePlatform).detect()).resolves.toBe(platform);
  });

  it('fails loudly when PLATFORM is read before detection', () => {
    expect(() => TestBed.inject(ActivePlatform).get()).toThrow(/before the platform detection/);
  });
});
