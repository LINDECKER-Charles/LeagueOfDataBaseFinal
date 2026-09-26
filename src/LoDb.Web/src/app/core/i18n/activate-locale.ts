import { DOCUMENT, inject } from '@angular/core';
import type { ResolveFn } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DEFAULT_LOCALE } from './default-locale';
import { isLocale } from './is-locale';
import type { Locale } from './locales';

/**
 * Activates the locale of the URL before its pages render: `<html lang>` is set in the SSR
 * output, and the catalogue is loaded so the first render is already translated. A catalogue
 * that fails to load must not take the page down: keys then fall back to `en`.
 * The text direction is left to L3.2, which owns RTL.
 */
export const activateLocale: ResolveFn<Locale> = async (route) => {
  const requested = route.paramMap.get('locale');
  const locale = isLocale(requested) ? requested : DEFAULT_LOCALE;
  const transloco = inject(TranslocoService);

  inject(DOCUMENT).documentElement.lang = locale;
  transloco.setActiveLang(locale);
  await firstValueFrom(transloco.load(locale)).catch(() => undefined);
  return locale;
};
