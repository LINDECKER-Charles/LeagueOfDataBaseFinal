import type { ResolveFn } from '@angular/router';
import { DEFAULT_LOCALE } from './default-locale';
import { injectLocaleSwitch } from './inject-locale-switch';
import { isLocale } from './is-locale';
import type { Locale } from './locales';

/**
 * Activates the locale of the URL before its pages render: `<html lang>` is set in the SSR
 * output, and the catalogue is loaded so the first render is already translated.
 * The text direction is left to L3.2, which owns RTL.
 */
export const activateLocale: ResolveFn<Locale> = async (route) => {
  const requested = route.paramMap.get('locale');
  const locale = isLocale(requested) ? requested : DEFAULT_LOCALE;
  await injectLocaleSwitch()(locale);
  return locale;
};
