import { DEFAULT_THEME } from './default-theme';
import { THEME_COOKIE } from './theme-cookie';
import { THEMES, type Theme } from './themes';

/**
 * Theme named by a `document.cookie` string. Absent, unknown or tampered values give the
 * default: the attribute is always set, never left empty. The inline script of
 * src/index.html implements the same rule in ES5; the spec runs both on the same inputs.
 */
export function themeFromCookie(cookies: string): Theme {
  const prefix = `${THEME_COOKIE}=`;
  const value = cookies
    .split(';')
    .map((entry) => entry.trim())
    .find((entry) => entry.startsWith(prefix))
    ?.slice(prefix.length);

  return THEMES.find((theme) => theme === value) ?? DEFAULT_THEME;
}
