import type { SecureStoragePlugin } from '@aparajita/capacitor-secure-storage';
import type { AppPlugin } from '@capacitor/app';
import type { BrowserPlugin } from '@capacitor/browser';
import type { SharePlugin } from '@capacitor/share';

/**
 * The part of the Capacitor plugins the Android platform uses (L10.1 installs them). Kept
 * this narrow so a spec can simulate every one of them without a WebView.
 */
export interface AndroidPlugins {
  readonly app: Pick<AppPlugin, 'addListener' | 'getInfo' | 'getLaunchUrl' | 'minimizeApp'>;
  readonly browser: Pick<BrowserPlugin, 'open' | 'close'>;
  readonly share: Pick<SharePlugin, 'share'>;
  /** Backed by the Android Keystore (ADR 0009): where the refresh token lives. */
  readonly secureStorage: Pick<SecureStoragePlugin, 'getItem' | 'setItem' | 'removeItem'>;
}
