import type { PlatformKind } from '../platform-kind';

/** Globals the hosts inject before the application starts; nothing else is trusted. */
interface HostGlobals {
  /** Set by the Photino host in the index.html it serves (plan, section 5.2). */
  readonly __LODB_DESKTOP__?: unknown;
  /** Set by the Capacitor native bridge, only inside the Android WebView. */
  readonly Capacitor?: { readonly getPlatform?: () => unknown };
}

function isDesktopMarker(marker: unknown): boolean {
  return (
    typeof marker === 'object' &&
    marker !== null &&
    'version' in marker &&
    typeof marker.version === 'string'
  );
}

/**
 * Tells the platform from the globals of the page, or `web` without any (server rendering).
 * Reading the bridge global rather than importing `@capacitor/core` keeps native code out of
 * the web bundle. The two hosts never coexist; the desktop marker is simply checked first.
 */
export function detectPlatformKind(globals: object | null): PlatformKind {
  const host = (globals ?? {}) as HostGlobals;
  if (isDesktopMarker(host.__LODB_DESKTOP__)) {
    return 'desktop';
  }
  return host.Capacitor?.getPlatform?.() === 'android' ? 'android' : 'web';
}
