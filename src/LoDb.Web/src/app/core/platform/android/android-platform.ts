import { DOCUMENT, Injectable, type Signal, inject, signal } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { AuthStrategies } from '../../auth/strategy/auth-strategies';
import type { AuthStrategyKind } from '../auth-strategy-kind';
import type { PlatformKind } from '../platform-kind';
import type { PlatformService } from '../platform-service';
import type { ShareOutcome } from '../share-outcome';
import type { UpdateState } from '../update-state';
import { BearerAuthStrategy } from './auth/bearer-auth-strategy';
import { AndroidBackButton } from './lifecycle/android-back-button';
import { AndroidLifecycle } from './lifecycle/android-lifecycle';
import { ANDROID_PLUGINS } from './native/android-plugins-token';
import { GoogleReturnListener } from './oauth/google-return-listener';
import { isShareDismissal } from './share/is-share-dismissal';
import { isWebUrl } from './share/is-web-url';
import { AndroidUpdates } from './updates/android-updates';

/**
 * The Capacitor Android app (ADR 0007): pages served by the WebView from https://localhost,
 * the API on its public origin, links in the system browser, the native share sheet, and
 * `bearer` tokens (L10.2), and its updates (L10.3). Built once by the detection, which loads
 * this file lazily: the native plugins never reach the web bundle.
 */
@Injectable({ providedIn: 'root' })
export class AndroidPlatform implements PlatformService {
  readonly kind: PlatformKind = 'android';
  readonly authStrategy: AuthStrategyKind = 'bearer';
  private readonly document = inject(DOCUMENT);
  private readonly plugins = inject(ANDROID_PLUGINS);
  private readonly updates = inject(AndroidUpdates);
  readonly updateState: Signal<UpdateState> = this.updates.state;
  private readonly nativeVersion = signal<string | null>(null);

  // Everything here must be in place before the first navigation, which the detection
  // precedes: the strategy for the session read, the listeners for a cold App Link start,
  // the updates, which confirm the start of the bundle before the plugin's `readyTimeout`
  // rolls it back. Nothing built here may read `API_BASE_URL`: it comes from `PLATFORM`,
  // not yet set.
  constructor() {
    inject(AuthStrategies).register('bearer', BearerAuthStrategy);
    inject(AndroidLifecycle).start();
    inject(AndroidBackButton).start();
    inject(GoogleReturnListener).listen();
    // After the lifecycle: the updates check again on each return to the foreground.
    this.updates.start();
    this.plugins.app
      .getInfo()
      .then(({ version }) => this.nativeVersion.set(version))
      .catch(() => undefined);
  }

  // The WebView serves the shell from https://localhost: the API is on its public origin.
  apiOrigin(): string {
    return environment.publicApiOrigin ?? this.document.location.origin;
  }

  /**
   * `android/{versionName}` (ADR 0008). The native version is read asynchronously: until it
   * answers, a request leaves without the header, which the API treats as the web's and
   * never refuses with a 426.
   */
  clientHeader(): string | null {
    const version = this.nativeVersion();
    return version === null ? null : `android/${version}`;
  }

  async openExternal(url: string): Promise<void> {
    if (!isWebUrl(url)) {
      throw new Error('Only http(s) links can be opened outside the application.');
    }
    await this.plugins.browser.open({ url });
  }

  // The WebView has no download manager, and writing to the device needs a filesystem
  // plugin the app does not ship: nothing calls this on Android yet.
  async saveFile(_file: File): Promise<void> {
    throw new Error('Saving a file is not available in the Android app.');
  }

  async share(content: ShareData): Promise<ShareOutcome> {
    try {
      await this.plugins.share.share({
        title: content.title,
        text: content.text,
        url: content.url,
      });
      return 'shared';
    } catch (error) {
      if (isShareDismissal(error)) {
        return 'dismissed';
      }
      throw error;
    }
  }

  applyUpdate(): Promise<void> {
    return this.updates.apply();
  }
}
