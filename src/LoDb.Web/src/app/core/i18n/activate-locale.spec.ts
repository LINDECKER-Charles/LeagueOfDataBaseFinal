import { DOCUMENT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, type RouterStateSnapshot } from '@angular/router';
import {
  provideTransloco,
  type Translation,
  type TranslocoLoader,
  TranslocoService,
} from '@jsverse/transloco';
import { type Observable, of, throwError } from 'rxjs';
import { activateLocale } from './activate-locale';
import { LOCALES } from './locales';

const CATALOGUES: Record<string, Translation> = {
  en: { base: { title: 'Hello' } },
  fr: { base: { title: 'Bonjour' } },
};

class InMemoryLoader implements TranslocoLoader {
  getTranslation(locale: string): Observable<Translation> {
    const catalogue = CATALOGUES[locale];
    return catalogue ? of(catalogue) : throwError(() => new Error(`No catalogue for ${locale}`));
  }
}

describe('activateLocale', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
    TestBed.configureTestingModule({
      providers: [
        provideTransloco({
          config: {
            availableLangs: [...LOCALES],
            defaultLang: 'en',
            fallbackLang: 'en',
            failedRetries: 0,
            missingHandler: { useFallbackTranslation: true, logMissingKey: false },
            prodMode: true,
          },
          loader: InMemoryLoader,
        }),
      ],
    });
  });

  // The document outlives the test: a locale left on <html lang> leaks into the next spec.
  afterEach(() => {
    vi.restoreAllMocks();
    document.documentElement.lang = lang;
  });

  function activate(locale: string): Promise<unknown> {
    const route = new ActivatedRouteSnapshot();
    route.params = { locale };
    return TestBed.runInInjectionContext(() =>
      activateLocale(route, {} as RouterStateSnapshot),
    ) as Promise<unknown>;
  }

  it('sets <html lang> and loads the catalogue before the page renders', async () => {
    await expect(activate('fr')).resolves.toBe('fr');

    const transloco = TestBed.inject(TranslocoService);
    expect(TestBed.inject(DOCUMENT).documentElement.lang).toBe('fr');
    expect(transloco.getActiveLang()).toBe('fr');
    expect(transloco.translate('base.title')).toBe('Bonjour');
  });

  it('still renders when the catalogue is missing, in en', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);

    await expect(activate('de')).resolves.toBe('de');

    expect(TestBed.inject(DOCUMENT).documentElement.lang).toBe('de');
    expect(TestBed.inject(TranslocoService).translate('base.title')).toBe('Hello');
  });
});
