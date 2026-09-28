import { DOCUMENT, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { Locale } from './locales';

/** Switches the page to a locale: `<html lang>`, then its catalogue, then the active locale. */
export type LocaleSwitch = (locale: Locale) => Promise<void>;

/**
 * The locale is made active only once its catalogue has loaded, never before. The chrome
 * renders before the first navigation resolves and loads its scopes alongside the root
 * catalogue; Transloco counts a locale as loaded as soon as one of its scopes lands. A pipe
 * re-rendered by an early activation then reads its root keys from a catalogue still in
 * flight, and keeps the raw key (or the `en` text) once it lands: no event follows. Activated
 * after the load, every pipe re-renders against the complete catalogue.
 *
 * A catalogue that fails to load must not take the page down: keys then fall back to `en`.
 */
export function injectLocaleSwitch(): LocaleSwitch {
  const document = inject(DOCUMENT);
  const transloco = inject(TranslocoService);
  return async (locale) => {
    document.documentElement.lang = locale;
    await firstValueFrom(transloco.load(locale)).catch(() => undefined);
    transloco.setActiveLang(locale);
  };
}
