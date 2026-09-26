import { provideHttpClient } from '@angular/common/http';
import { type ApplicationConfig, inject, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideClientHydration } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { API_BASE_URL } from './core/api/api-base-url';
import { provideI18n } from './core/i18n/provide-i18n';
import { PLATFORM } from './core/platform/platform';
import { providePlatform } from './core/platform/provide-platform';

/**
 * Browser configuration, shared with the server (app.config.server.ts). Angular 22 already
 * makes fetch the HttpClient backend and turns incremental hydration on with
 * `provideClientHydration()`: `withFetch()` and `withIncrementalHydration()` are deprecated
 * there. The transfer cache defaults fit as they are: GET and HEAD only, never a request
 * carrying credentials, never a response setting a cookie. L3.1 adds the interceptors.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(),
    provideClientHydration(),
    providePlatform(),
    { provide: API_BASE_URL, useFactory: () => inject(PLATFORM).apiOrigin() },
    provideI18n(),
  ],
};
