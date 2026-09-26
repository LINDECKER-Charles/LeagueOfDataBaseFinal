import type { Locale } from '../../i18n/locales';

/**
 * Absolute URL of a page under its locale prefix (ADR 0005): `/fr` for the home page,
 * `/fr/about/data` below it. Built as one string because a router command only splits the
 * first segment on slashes.
 */
export function localePath(locale: Locale, path: string): string {
  return path === '' ? `/${locale}` : `/${locale}/${path}`;
}
