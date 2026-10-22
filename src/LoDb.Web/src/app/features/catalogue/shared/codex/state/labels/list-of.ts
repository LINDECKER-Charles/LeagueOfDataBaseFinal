import type { Locale } from '../../../../../../core/i18n/locales';

/**
 * Labels or values as one phrase, as the locale separates the items of a list ("A, B, C",
 * "A、B、C"), without an "and" where it can do without.
 */
export function listOf(locale: Locale, texts: readonly string[]): string {
  return new Intl.ListFormat(locale, { type: 'conjunction', style: 'narrow' }).format(texts);
}
