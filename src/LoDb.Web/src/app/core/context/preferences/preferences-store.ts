import { isPlatformBrowser } from '@angular/common';
import { DOCUMENT, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { preferencesCookieEntry } from './preferences-cookie-entry';
import { preferencesFromCookie } from './preferences-from-cookie';
import { preferencesFromValue } from './preferences-from-value';
import { preferencesValue } from './preferences-value';
import type { Preferences } from './preferences';

// The choice of this browsing session, the legacy PHP session's part: no cookie, so the
// cookie policy's "set only if you tick remember" stays true.
const SESSION_KEY = 'lodb.context';

/**
 * The remembered context, in the browser only: on the server it reads nothing and writes
 * nothing, so a render never depends on who asks. The switcher (L3.9) keeps every choice for
 * the browsing session, and writes the cookie only when the visitor ticks "remember"; the URL
 * still wins over both (ADR 0005: path > query > cookie).
 */
@Injectable({ providedIn: 'root' })
export class PreferencesStore {
  private readonly document = inject(DOCUMENT);
  private readonly inBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  // A signal, so the chrome and the switcher follow a choice applied without a navigation
  // (same URL), as the legacy reload did.
  private readonly kept = signal<Preferences | null>(this.readSession());

  /** What the cookie remembers across visits. */
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

  /**
   * The context to fill a URL with: this session's choice, else the remembered one. Reactive
   * to `keep`; the cookie alone is read as it stands.
   */
  current(): Preferences | null {
    return this.kept() ?? this.read();
  }

  /** Remembers `preferences` for a year, or forgets them when null. */
  remember(preferences: Preferences | null): void {
    if (this.inBrowser) {
      const secure = this.document.location.protocol === 'https:';
      this.document.cookie = preferencesCookieEntry(preferences, secure);
    }
  }

  /** Keeps `preferences` until the browsing session ends, "remember" ticked or not. */
  keep(preferences: Preferences): void {
    try {
      this.session()?.setItem(SESSION_KEY, preferencesValue(preferences));
    } catch {
      // Storage refused (private mode, quota): the choice lives until the page reloads.
    }
    this.kept.set(preferences);
  }

  private readSession(): Preferences | null {
    try {
      const stored = this.session()?.getItem(SESSION_KEY) ?? null;
      return stored === null ? null : preferencesFromValue(stored);
    } catch {
      return null;
    }
  }

  private session(): Storage | null {
    return this.inBrowser ? (this.document.defaultView?.sessionStorage ?? null) : null;
  }
}
