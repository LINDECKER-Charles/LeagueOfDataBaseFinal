import { isPlatformBrowser } from '@angular/common';
import { DOCUMENT, Injectable, PLATFORM_ID, inject } from '@angular/core';
import { preferencesCookieEntry } from './preferences-cookie-entry';
import { preferencesFromCookie } from './preferences-from-cookie';
import type { Preferences } from './preferences';

/**
 * The remembered context, in the browser only: on the server it reads nothing and writes
 * nothing, so a render never depends on who asks. The switcher (L3.9) writes it when the
 * visitor ticks "remember"; the URL still wins over it (ADR 0005: path > query > cookie).
 */
@Injectable({ providedIn: 'root' })
export class PreferencesStore {
  private readonly document = inject(DOCUMENT);
  private readonly inBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  read(): Preferences | null {
    if (!this.inBrowser) {
      return null;
    }
    try {
      return preferencesFromCookie(this.document.cookie);
    } catch {
      // Sandboxed documents throw on cookie access: nothing is remembered there.
      return null;
    }
  }

  /** Remembers `preferences` for a year, or forgets them when null. */
  remember(preferences: Preferences | null): void {
    if (this.inBrowser) {
      const secure = this.document.location.protocol === 'https:';
      this.document.cookie = preferencesCookieEntry(preferences, secure);
    }
  }
}
