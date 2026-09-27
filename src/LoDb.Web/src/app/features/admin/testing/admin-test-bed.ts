import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import type { EnvironmentProviders, Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type Routes, provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import { LOCALES } from '../../../core/i18n/locales';
import { CANONICAL_ORIGIN } from '../../../core/seo/canonical-origin';
import { ADMIN_API } from './admin-api';

/**
 * Configures the test bed of an admin spec: the API at `ADMIN_API`, Transloco without
 * catalogues (a text renders as its key, which the specs look for), and `routes`.
 */
export function configureAdminTestBed(
  routes: Routes = [],
  providers: readonly (Provider | EnvironmentProviders)[] = [],
): void {
  TestBed.configureTestingModule({
    providers: [
      { provide: API_BASE_URL, useValue: ADMIN_API },
      { provide: CANONICAL_ORIGIN, useValue: 'https://league-of-data-base.com' },
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter(routes),
      provideTransloco({
        config: {
          availableLangs: [...LOCALES],
          defaultLang: 'en',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = () => of({});
        },
      }),
      ...providers,
    ],
  });
}
