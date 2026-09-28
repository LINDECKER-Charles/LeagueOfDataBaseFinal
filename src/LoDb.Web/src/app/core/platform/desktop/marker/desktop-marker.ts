import type { BridgeTransport } from '../bridge/bridge-transport';

/**
 * What the desktop host injects in the `index.html` it serves (plan, section 5.2):
 * `window.__LODB_DESKTOP__ = {version, bridge?}`. No bridge in the smoke check, nor in the
 * system browser the host falls back to when the WebView cannot start.
 */
export interface DesktopMarker {
  /** The version of the app, as its release tag writes it. */
  readonly version: string;
  readonly bridge: BridgeTransport | null;
}
