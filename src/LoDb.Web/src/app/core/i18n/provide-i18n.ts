import { PlatformLocation } from '@angular/common';
import {
  type EnvironmentProviders,
  inject,
  isDevMode,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { TRANSLOCO_TRANSPILER, TranslocoService, provideTransloco } from '@jsverse/transloco';
import { DEFAULT_LOCALE } from './default-locale';
import { isLocale } from './is-locale';
import { LOCALES } from './locales';
import { TranslocoHttpLoader } from './loading/transloco-http-loader';
import { PrecompiledTranspiler } from './messages/precompiled-transpiler';

/**
 * Makes the locale of the URL the active one before the first render. The chrome sits outside
 * the router outlet and renders before the first navigation resolves: under the default
 * locale, hydration would paint its English labels over the server's own for a frame, and the
 * server would load the chrome's `en` scopes for nothing. Its catalogue is not awaited here,
 * since no request may leave before the platform is detected: the browser reads it from the
 * transfer cache at once, and `activateLocale` then loads it and activates the locale again,
 * which re-renders every label against the complete catalogue. A page outside the locale
 * prefix (a shared build) keeps the default until its resolver switches.
 */
function activateUrlLocale(): void {
  const [, segment] = inject(PlatformLocation).pathname.split('/');
  if (isLocale(segment)) {
    inject(TranslocoService).setActiveLang(segment);
  }
}

/**
 * Transloco with ICU messages. A missing key falls back to its `en` translation rather than
 * the whole page switching language (plan, section 5.2): the build completes every catalogue
 * with the `en` messages it lacks (`npm run i18n:build`), so a page loads its own locale's
 * catalogues only, never `en` beside them. The same build compiles the ICU messages, which
 * `PrecompiledTranspiler` renders.
 */
export function provideI18n(): EnvironmentProviders {
  return makeEnvironmentProviders([
    ...provideTransloco({
      config: {
        availableLangs: [...LOCALES],
        defaultLang: DEFAULT_LOCALE,
        fallbackLang: DEFAULT_LOCALE,
        missingHandler: { useFallbackTranslation: false, logMissingKey: isDevMode() },
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
      },
      loader: TranslocoHttpLoader,
    }),
    { provide: TRANSLOCO_TRANSPILER, useClass: PrecompiledTranspiler },
    provideAppInitializer(activateUrlLocale),
  ]);
}
