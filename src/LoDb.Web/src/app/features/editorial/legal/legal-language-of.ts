import type { LegalLanguage } from './legal-language';

/**
 * The text a locale reads: French for every `fr*` locale, the English reference version for
 * all the others.
 */
export function legalLanguageOf(locale: string): LegalLanguage {
  return locale.startsWith('fr') ? 'fr' : 'en';
}
