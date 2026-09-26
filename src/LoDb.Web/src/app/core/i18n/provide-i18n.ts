import { type EnvironmentProviders, isDevMode, makeEnvironmentProviders } from '@angular/core';
import { provideTransloco } from '@jsverse/transloco';
import { provideTranslocoMessageformat } from '@jsverse/transloco-messageformat';
import { DEFAULT_LOCALE } from './default-locale';
import { LOCALES } from './locales';
import { TranslocoHttpLoader } from './transloco-http-loader';

/**
 * Transloco with ICU messages. A missing key falls back to its `en` translation rather than
 * the whole page switching language (plan, section 5.2).
 */
export function provideI18n(): EnvironmentProviders {
  return makeEnvironmentProviders([
    ...provideTransloco({
      config: {
        availableLangs: [...LOCALES],
        defaultLang: DEFAULT_LOCALE,
        fallbackLang: DEFAULT_LOCALE,
        missingHandler: { useFallbackTranslation: true, logMissingKey: isDevMode() },
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
      },
      loader: TranslocoHttpLoader,
    }),
    // Per-key fallback renders `en` messages under every locale, and CLDR has no `one`
    // plural category in ja, ko, th, vi or zh: strict keys would throw on those messages.
    provideTranslocoMessageformat({ strictPluralKeys: false }),
  ]);
}
