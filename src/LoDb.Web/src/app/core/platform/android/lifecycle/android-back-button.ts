import { Dialog } from '@angular/cdk/dialog';
import { DOCUMENT, Injectable, inject } from '@angular/core';
import type { BackButtonListenerEvent } from '@capacitor/app';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';

/**
 * The system back button. Listening replaces Capacitor's default, which only walks the
 * WebView history: an open dialog or sheet closes first, as Escape does, then the history
 * goes back, and on the first page the app moves to the background rather than quitting,
 * as Android 12 and later do for a root activity.
 */
@Injectable({ providedIn: 'root' })
export class AndroidBackButton {
  private readonly plugins = inject(ANDROID_PLUGINS);
  private readonly dialog = inject(Dialog);
  private readonly document = inject(DOCUMENT);
  private isListening = false;

  /** Idempotent: the platform starts it once, at detection. */
  start(): void {
    if (this.isListening) {
      return;
    }
    this.isListening = true;
    void this.plugins.app.addListener('backButton', (event) => void this.goBack(event));
  }

  private async goBack({ canGoBack }: BackButtonListenerEvent): Promise<void> {
    const topDialog = this.dialog.openDialogs.at(-1);
    if (topDialog !== undefined) {
      topDialog.close();
      return;
    }
    if (canGoBack) {
      this.document.defaultView?.history.back();
      return;
    }
    await this.plugins.app.minimizeApp();
  }
}
