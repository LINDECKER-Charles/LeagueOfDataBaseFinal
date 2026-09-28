import type { ContextTarget } from '../../../core/context/switch/context-target';
import type { LanguageOption } from '../options/language-option';

/**
 * The context a submitted choice asks for: the option's locale, read in its language (null
 * for the locale's own, which the URL implies), and the version, which `switchContext`
 * leaves out of the URL when it is the latest.
 */
export function targetOf(version: string, language: LanguageOption): ContextTarget {
  return { locale: language.locale, version, lang: language.lang };
}
