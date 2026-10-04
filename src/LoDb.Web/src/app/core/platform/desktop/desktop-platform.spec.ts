import { provideHttpClient } from '@angular/common/http';
import type { EnvironmentProviders, Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../../api/api-base-url';
import { AuthStrategies } from '../../auth/strategy/auth-strategies';
import { HostAuthStrategy } from './auth/host-auth-strategy';
import { BridgeError } from './bridge/bridge-error';
import { DesktopPlatform } from './desktop-platform';
import type { DesktopMarker } from './marker/desktop-marker';
import { DESKTOP_MARKER } from './marker/desktop-marker-token';
import { FakeDesktopBridge } from './testing/fake-desktop-bridge';

describe('DesktopPlatform', () => {
  let host: FakeDesktopBridge;

  function start(
    marker: DesktopMarker = host.marker(),
    afterDetection: (Provider | EnvironmentProviders)[] = [],
  ): DesktopPlatform {
    // No API_BASE_URL: the app derives it from PLATFORM, which the detection only sets once
    // this platform is built. Anything built here that reads it fails these specs.
    TestBed.configureTestingModule({
      providers: [{ provide: DESKTOP_MARKER, useValue: marker }, ...afterDetection],
    });
    return TestBed.inject(DesktopPlatform);
  }

  beforeEach(() => {
    host = new FakeDesktopBridge();
    host.answer = ({ type }) =>
      type === 'updateState'
        ? { ok: true, result: { state: 'ready', version: '1.5.0' } }
        : { ok: true, result: {} };
  });

  it('is the desktop app, authenticated by its host, on the page origin', () => {
    const platform = start(host.marker('1.4.0-beta.2'));

    expect(platform.kind).toBe('desktop');
    expect(platform.authStrategy).toBe('host');
    expect(platform.apiOrigin()).toBe(document.location.origin);
    expect(platform.clientHeader()).toBe('desktop/1.4.0-beta.2');
  });

  it('registers the host strategy with core/auth as it is built', () => {
    start(host.marker(), [
      provideHttpClient(),
      provideRouter([]),
      { provide: API_BASE_URL, useValue: document.location.origin },
    ]);

    expect(TestBed.inject(AuthStrategies).get('host')).toBeInstanceOf(HostAuthStrategy);
  });

  it('announces the update the host has ready, and restarts on it', async () => {
    const platform = start();

    await vi.waitFor(() => expect(platform.updateState()).toBe('ready'));
    await platform.applyUpdate();

    expect(host.types()).toEqual(['updateState', 'applyUpdate']);
  });

  it('opens web links through the host', async () => {
    await start().openExternal('https://www.leagueoflegends.com/');

    const opened = host.sent.find(({ type }) => type === 'openExternal');
    expect(opened?.payload).toEqual({ url: 'https://www.leagueoflegends.com/' });
  });

  it.each(['javascript:alert(1)', 'file:///etc/passwd', 'nope'])(
    'refuses to open anything but a web link (%s)',
    async (url) => {
      await expect(start().openExternal(url)).rejects.toThrow(/http\(s\)/);
      expect(host.types()).not.toContain('openExternal');
    },
  );

  it('saves a file through the host dialog', async () => {
    const file = new File(['{}'], 'build.json', { type: 'application/json' });

    await start().saveFile(file);

    const saved = host.sent.find(({ type }) => type === 'saveFile');
    expect(saved?.payload).toEqual({
      name: 'build.json',
      mime: 'application/json',
      base64: 'e30=',
    });
  });

  it('fails with the code the host refused a request with', async () => {
    host.answer = ({ type }) =>
      type === 'saveFile' ? { ok: false, error: 'busy' } : { ok: true, result: {} };

    await expect(start().saveFile(new File(['x'], 'x.txt'))).rejects.toEqual(
      new BridgeError('busy'),
    );
  });

  it('shares a link by copying it', async () => {
    const writeText = vi.fn(() => Promise.resolve());
    // jsdom has no clipboard: the WebView's is lent to the page for this spec only.
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
    onTestFinished(() => {
      Reflect.deleteProperty(navigator, 'clipboard');
    });

    const platform = start();

    await expect(platform.share({ title: 'Jinx', url: 'https://x.example/b/1' })).resolves.toBe(
      'copied',
    );
    await expect(platform.share({ text: 'no link' })).rejects.toThrow(/link/);
    expect(writeText).toHaveBeenCalledExactlyOnceWith('https://x.example/b/1');
  });

  it('asks the host nothing without a bridge, as in the smoke check', async () => {
    const platform = start({ version: '1.4.0', bridge: null });

    expect(platform.updateState()).toBe('none');
    await expect(platform.openExternal('https://x.example/')).rejects.toEqual(
      new BridgeError('unavailable'),
    );
    expect(host.sent).toEqual([]);
  });

  it('keeps nothing in the page storage', async () => {
    const platform = start();
    await platform.saveFile(new File(['x'], 'x.txt'));

    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });
});
