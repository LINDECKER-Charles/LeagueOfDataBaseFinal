import { ApplicationRef, effect, inject, untracked } from '@angular/core';
import { AuthSession } from '../../../../core/auth/session/auth-session';

/**
 * Runs `read` whenever a reader is signed in and `source` changes, and once when the session
 * turns out to be signed in: the server renders the page anonymous and shared (ADR 0005), so
 * the reader's own votes are read again in the browser, once the application is stable, when
 * the transfer cache no longer answers with the server's anonymous copy. Nothing runs on the
 * server, whose session always stays unknown. Called in a constructor.
 */
export function whenSignedIn<T>(source: () => T, read: (value: T) => void): void {
  const session = inject(AuthSession);
  const application = inject(ApplicationRef);
  effect(() => {
    const value = source();
    if (session.status() === 'authenticated') {
      void application.whenStable().then(() => untracked(() => read(value)));
    }
  });
}
