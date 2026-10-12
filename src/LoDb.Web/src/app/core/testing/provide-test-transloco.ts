import type { EnvironmentProviders } from '@angular/core';
import { provideTransloco, type Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import { DEFAULT_LOCALE } from '../i18n/default-locale';
import { LOCALES, type Locale } from '../i18n/locales';

/** What the Transloco of a spec loads, and in which language it starts. */
export interface TestTranslations {
  /** The catalogue of a Transloco path: `en`, `fr`, `patches/fr`… */
  readonly byPath?: Readonly<Record<string, Translation>>;
  /** What any other path loads. Nothing by default: a text then renders as its key. */
  readonly otherwise?: Translation;
  /** The language active from the start. */
  readonly lang?: Locale;
}

/**
 * Transloco as a spec runs it: the site's locales, catalogues served at once from memory,
 * a missing key left silent. Here rather than in each spec: a feature imports `core/`.
 */
export function provideTestTransloco({
  byPath = {},
  otherwise = {},
  lang = DEFAULT_LOCALE,
}: TestTranslations = {}): EnvironmentProviders[] {
  return provideTransloco({
    config: {
      availableLangs: [...LOCALES],
      defaultLang: lang,
      missingHandler: { logMissingKey: false },
      prodMode: true,
    },
    loader: class {
      getTranslation = (path: string) => of(byPath[path] ?? otherwise);
    },
  });
}
