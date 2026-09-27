import type { Locale } from '../../../core/i18n/locales';

/** One entry of the language list: an interface locale read in one Data Dragon language. */
export interface LanguageOption {
  /** Value of the `<option>`, unique in the list: `{locale}:{language}`. */
  readonly key: string;
  readonly locale: Locale;
  /** Data Dragon language the pages read, such as `en_GB`. */
  readonly language: string;
  /** What `?lang=` carries: null for the locale's own language, the variant otherwise. */
  readonly lang: string | null;
  /** English name of the language, as the legacy switcher listed it. */
  readonly label: string;
}
