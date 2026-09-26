import type { Direction } from '@angular/cdk/bidi';
import type { Locale } from '../../i18n/locales';

// Arabic is the one right-to-left script among the 21 site locales.
const RIGHT_TO_LEFT_LOCALES: readonly Locale[] = ['ar'];

/** Writing direction of a site locale, for `<html dir>` and the CDK's Directionality. */
export function textDirection(locale: Locale): Direction {
  return RIGHT_TO_LEFT_LOCALES.includes(locale) ? 'rtl' : 'ltr';
}
