import { detectPlatformKind } from './detect-platform-kind';

describe('detectPlatformKind', () => {
  const android = { getPlatform: () => 'android' };

  it('answers web during server rendering, where there is no window', () => {
    expect(detectPlatformKind(null)).toBe('web');
  });

  it('answers web in a plain browser', () => {
    expect(detectPlatformKind({})).toBe('web');
    expect(detectPlatformKind(window)).toBe('web');
  });

  it('answers desktop when the Photino host injected its marker', () => {
    expect(detectPlatformKind({ __LODB_DESKTOP__: { version: '1.4.0' } })).toBe('desktop');
  });

  it.each([null, 'desktop', true, {}, { version: 14 }])(
    'ignores a malformed desktop marker (%j)',
    (marker) => {
      expect(detectPlatformKind({ __LODB_DESKTOP__: marker })).toBe('web');
    },
  );

  it('answers android inside the Capacitor Android WebView', () => {
    expect(detectPlatformKind({ Capacitor: android })).toBe('android');
  });

  it('answers web when Capacitor runs in a browser or looks incomplete', () => {
    expect(detectPlatformKind({ Capacitor: { getPlatform: () => 'web' } })).toBe('web');
    expect(detectPlatformKind({ Capacitor: { getPlatform: () => 'ios' } })).toBe('web');
    expect(detectPlatformKind({ Capacitor: {} })).toBe('web');
  });

  it('checks the desktop marker first', () => {
    const both = { __LODB_DESKTOP__: { version: '1.4.0' }, Capacitor: android };
    expect(detectPlatformKind(both)).toBe('desktop');
  });
});
