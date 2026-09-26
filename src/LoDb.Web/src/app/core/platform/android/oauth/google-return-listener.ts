import { Injectable, Injector, inject } from '@angular/core';
import { ActivePlatform } from '../../detection/active-platform';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';
import { AndroidGoogleSignIn } from './android-google-sign-in';

/**
 * Hands every App Link to the Google sign-in: the one that reaches the running app, and the
 * one that started it cold. It listens from the platform's construction, during detection,
 * so it holds nothing that needs the API origin (`API_BASE_URL` reads `PLATFORM`): the
 * sign-in is only built once the detection has completed.
 */
@Injectable({ providedIn: 'root' })
export class GoogleReturnListener {
  private readonly plugins = inject(ANDROID_PLUGINS);
  private readonly injector = inject(Injector);
  private readonly activePlatform = inject(ActivePlatform);
  private isListening = false;

  /** Idempotent: the platform starts it once, at detection. */
  listen(): void {
    if (this.isListening) {
      return;
    }
    this.isListening = true;
    void this.plugins.app.addListener('appUrlOpen', ({ url }) => void this.complete(url));
    // Android answers nothing without a launch link, the web fallback of the plugin an empty one.
    void this.plugins.app.getLaunchUrl().then((launch) => {
      if (launch?.url) {
        void this.complete(launch.url);
      }
    });
  }

  private async complete(url: string): Promise<void> {
    await this.activePlatform.detect();
    await this.injector.get(AndroidGoogleSignIn).complete(url);
  }
}
