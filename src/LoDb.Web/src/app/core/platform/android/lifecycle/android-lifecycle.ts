import { Injectable, inject } from '@angular/core';
import { type Observable, Subject } from 'rxjs';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';

/**
 * The app coming back to the foreground. The live update of L10.3 checks for a new bundle on
 * each return; nothing else needs it yet, as an expired access token renews on its own.
 */
@Injectable({ providedIn: 'root' })
export class AndroidLifecycle {
  private readonly plugins = inject(ANDROID_PLUGINS);
  private readonly resumed = new Subject<void>();
  private isListening = false;

  /** Emits each time the app returns to the foreground, never on the cold start. */
  readonly resumes: Observable<void> = this.resumed.asObservable();

  /** Idempotent: the platform starts it once, at detection. */
  start(): void {
    if (this.isListening) {
      return;
    }
    this.isListening = true;
    void this.plugins.app.addListener('resume', () => this.resumed.next());
  }
}
