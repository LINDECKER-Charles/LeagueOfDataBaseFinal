import { detectPlatformKind } from '../../detection/detect-platform-kind';
import { readDesktopMarker } from './read-desktop-marker';

describe('readDesktopMarker', () => {
  const bridge = { send: () => undefined, listen: () => undefined };

  it('reads the version and the bridge the host injected', () => {
    const marker = readDesktopMarker({
      __LODB_DESKTOP__: Object.freeze({ version: '1.4.0', bridge }),
    });

    expect(marker).toEqual({ version: '1.4.0', bridge });
  });

  it('keeps the marker without a usable bridge, as in the smoke check', () => {
    expect(readDesktopMarker({ __LODB_DESKTOP__: { version: '1.4.0' } })?.bridge).toBeNull();
    expect(
      readDesktopMarker({ __LODB_DESKTOP__: { version: '1.4.0', bridge: { send: 'x' } } })?.bridge,
    ).toBeNull();
  });

  it.each([null, {}, { __LODB_DESKTOP__: true }, { __LODB_DESKTOP__: { version: 140 } }])(
    'finds no marker in %o, which the detection reads as another platform',
    (globals) => {
      expect(readDesktopMarker(globals)).toBeNull();
      expect(detectPlatformKind(globals)).not.toBe('desktop');
    },
  );

  it('agrees with the detection on a desktop page', () => {
    const globals = { __LODB_DESKTOP__: { version: '1.4.0-beta.2', bridge } };

    expect(detectPlatformKind(globals)).toBe('desktop');
    expect(readDesktopMarker(globals)?.version).toBe('1.4.0-beta.2');
  });
});
