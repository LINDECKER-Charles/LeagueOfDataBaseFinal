import { Injectable, inject, signal } from '@angular/core';
import { PLATFORM } from '../platform/platform';

/**
 * Restarts the app on its downloaded update, for the banner and the blocking screen: busy
 * while the platform works, failed when it could not, so the view can say so. Provided by
 * each component that uses it.
 */
@Injectable()
export class UpdateRestart {
  private readonly platform = inject(PLATFORM, { optional: true });
  readonly busy = signal(false);
  readonly failed = signal(false);

  async restart(): Promise<void> {
    this.busy.set(true);
    this.failed.set(false);
    try {
      // On success the app closes: nothing after this line runs on the old version.
      await this.platform?.applyUpdate();
    } catch {
      this.failed.set(true);
    } finally {
      this.busy.set(false);
    }
  }
}
