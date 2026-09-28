import { LocationStrategy } from '@angular/common';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { type ApplicationConfig, inject, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideClientHydration } from '@angular/platform-browser';
import {
  provideRouter,
  withInMemoryScrolling,
  withNavigationErrorHandler,
  withRouterConfig,
} from '@angular/router';
import { routes } from './app.routes';
import { provideNavigationBeacon } from './core/analytics/provide-navigation-beacon';
import { API_BASE_URL } from './core/api/api-base-url';
import { authInterceptor } from './core/auth/auth-interceptor';
import { provideAuth } from './core/auth/provide-auth';
import { clientHeaderInterceptor } from './core/http/interceptors/client-header-interceptor';
import { serverApiTimeoutInterceptor } from './core/http/interceptors/server-api-timeout-interceptor';
import { provideI18n } from './core/i18n/provide-i18n';
import { PLATFORM } from './core/platform/platform';
import { providePlatform } from './core/platform/provide-platform';
import { handleNavigationError } from './core/routing/failure/handle-navigation-error';
import { LocaleHomeLocationStrategy } from './core/routing/locale/locale-home-location-strategy';
import { provideUpdates } from './core/update/provide-updates';
import { updateInterceptor } from './core/update/update-interceptor';
import { PORTAL_PAYMENTS } from './features/api-portal/shared/portal-payments';
import { provideReleaseVersion } from './features/editorial/changelog/provide-release-version';
import { PAYMENTS_ENABLED } from '../environments/payments-enabled';

/**
 * Browser configuration, shared with the server (app.config.server.ts). Angular 22 already
 * makes fetch the HttpClient backend and turns incremental hydration on with
 * `provideClientHydration()`: `withFetch()` and `withIncrementalHydration()` are deprecated
 * there. The transfer cache defaults fit as they are: GET and HEAD only, never a request
 * carrying credentials, never a response setting a cookie.
 *
 * Router: the resolvers read `locale` and `version` from their parent routes; a navigation
 * that fails renders its 500 or 503 page in place (core/routing/failure), and one whose
 * error page fails too resolves false instead of rejecting. Component input binding stays
 * off: pages read their data with `injectRouteData`. A new page starts at its top, history
 * restores its position; a filter that rewrites its query navigates with `scroll: 'manual'`.
 * The address of a locale's home keeps its trailing slash (`LocaleHomeLocationStrategy`).
 *
 * `provideAuth`, `provideNavigationBeacon`, `provideUpdates` and the `auth` and `update`
 * interceptors are registered empty, for L4.5, L7.1 and L9.0 to fill in their own folders.
 *
 * The key portal sells packs and plans only where the build shows payments: a feature may
 * not read src/environments, so its `PORTAL_PAYMENTS` follows `PAYMENTS_ENABLED` here.
 * Likewise core may not read the changelog: the release the header chip and the footer show
 * (`RELEASE_VERSION`) is fed here by the changelog's `provideReleaseVersion`.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withRouterConfig({
        paramsInheritanceStrategy: 'always',
        resolveNavigationPromiseOnError: true,
      }),
      withNavigationErrorHandler(handleNavigationError),
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled' }),
    ),
    { provide: LocationStrategy, useClass: LocaleHomeLocationStrategy },
    provideHttpClient(
      withInterceptors([
        clientHeaderInterceptor,
        authInterceptor,
        updateInterceptor,
        serverApiTimeoutInterceptor,
      ]),
    ),
    provideClientHydration(),
    providePlatform(),
    { provide: API_BASE_URL, useFactory: () => inject(PLATFORM).apiOrigin() },
    provideI18n(),
    provideAuth(),
    provideNavigationBeacon(),
    provideUpdates(),
    { provide: PORTAL_PAYMENTS, useFactory: () => inject(PAYMENTS_ENABLED) },
    provideReleaseVersion(),
  ],
};
