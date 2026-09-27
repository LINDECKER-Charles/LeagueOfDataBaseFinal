import { DOCUMENT, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Meta } from '@angular/platform-browser';
import { DEFAULT_THEME } from './default-theme';
import { themeCookieEntry } from './theme-cookie-entry';
import { themeFromCookie } from './theme-from-cookie';
import { THEME_IDENTITIES } from './theme-identities';
import { THEMES, type Theme } from './themes';

const THEME_ATTRIBUTE = 'data-theme';
const BROWSER_COLOR_META = 'theme-color';

/**
 * The visitor's identity. The inline script of index.html paints it before the first frame;
 * this service reads the same cookie so the picker agrees with the page, then owns every
 * later change. The server never reads the cookie: it always answers the default, so the
 * rendered HTML never depends on the theme. A page may pin an identity over the visitor's
 * own for as long as it is shown, as the admin keeps Hextech (legacy admin stylesheets).
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly meta = inject(Meta);
  private readonly inBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly theme = signal<Theme>(this.inBrowser ? this.readCookie() : DEFAULT_THEME);
  private pinned: Theme | null = null;

  readonly current = this.theme.asReadonly();

  constructor() {
    this.paintBrowserChrome(this.theme());
  }

  /**
   * Paints `theme`, remembers it for a year and recolours the browser chrome. A value outside
   * the list (a tampered attribute, a stale bookmark) is ignored rather than written.
   */
  select(theme: Theme): void {
    if (!THEMES.includes(theme)) {
      return;
    }
    this.theme.set(theme);
    this.document.cookie = themeCookieEntry(theme, this.document.location.protocol === 'https:');
    if (this.pinned === null) {
      this.paint(theme);
    }
  }

  /** Paints `theme` until `unpin`, without remembering it nor making it the current one. */
  pin(theme: Theme): void {
    this.pinned = theme;
    this.paint(theme);
  }

  /** Paints the visitor's own identity again, if one was pinned over it. */
  unpin(): void {
    if (this.pinned !== null) {
      this.pinned = null;
      this.paint(this.theme());
    }
  }

  private paint(theme: Theme): void {
    this.document.documentElement.setAttribute(THEME_ATTRIBUTE, theme);
    this.paintBrowserChrome(theme);
  }

  private readCookie(): Theme {
    try {
      return themeFromCookie(this.document.cookie);
    } catch {
      // Sandboxed documents throw on cookie access: the default identity still applies.
      return DEFAULT_THEME;
    }
  }

  private paintBrowserChrome(theme: Theme): void {
    const content = THEME_IDENTITIES[theme].browserColor;
    this.meta.updateTag({ name: BROWSER_COLOR_META, content });
  }
}
