import { THEME_COOKIE } from './theme-cookie';
import type { Theme } from './themes';

// The choice outlives the session by a year, as it did in the legacy application.
const ONE_YEAR_IN_SECONDS = 365 * 24 * 60 * 60;

/**
 * `document.cookie` assignment that remembers a theme. Readable by the inline script, hence
 * no HttpOnly; Lax because nothing cross-site needs it; Secure whenever the page is.
 */
export function themeCookieEntry(theme: Theme, secure: boolean): string {
  const attributes = [`path=/`, `max-age=${ONE_YEAR_IN_SECONDS}`, 'samesite=lax'];
  return [`${THEME_COOKIE}=${theme}`, ...attributes, ...(secure ? ['secure'] : [])].join('; ');
}
