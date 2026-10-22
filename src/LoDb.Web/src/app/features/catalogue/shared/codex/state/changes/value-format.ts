import type { Locale } from '../../../../../../core/i18n/locales';
import type { Translate } from '../../texts/translate';

/** What the values of a change are written with: the page's locale and its texts. */
export interface ValueFormat {
  readonly locale: Locale;
  readonly translate: Translate;
}
