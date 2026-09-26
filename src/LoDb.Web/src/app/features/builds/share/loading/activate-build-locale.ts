import { DOCUMENT, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { Locale } from '../../../../core/i18n/locales';

/** Switches the page to a locale, awaited before it renders. */
export type LocaleActivation = (locale: Locale) => Promise<void>;

/**
 * What `activateLocale` does for the pages under `/{locale}`, for a shared build, which lives
 * outside them and speaks its own language: `<html lang>` in the SSR output, the active
 * catalogue, loaded before the first render. A catalogue that fails leaves its keys to `en`.
 * Injected up front, since the resolver learns the locale after awaiting the API.
 */
export function injectLocaleActivation(): LocaleActivation {
  const document = inject(DOCUMENT);
  const transloco = inject(TranslocoService);
  return async (locale) => {
    document.documentElement.lang = locale;
    transloco.setActiveLang(locale);
    await firstValueFrom(transloco.load(locale)).catch(() => undefined);
  };
}
