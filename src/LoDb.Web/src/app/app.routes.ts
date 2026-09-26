import { inject } from '@angular/core';
import type { CanMatchFn, Route, Routes } from '@angular/router';
import { PAYMENTS_ENABLED } from '../environments/payments-enabled';
import { activateLocale } from './core/i18n/activate-locale';
import { localeGuard } from './core/i18n/locale-guard';
import { redirectToPreferredLocale } from './core/routing/locale/redirect-to-preferred-locale';
import { isOutcomeNavigation } from './core/routing/outcome/is-outcome-navigation';
import { resolveOutcome } from './core/routing/outcome/resolve-outcome';
import { canMatchVersion } from './core/routing/version/can-match-version';

// Development gallery of the design system (L3.2), at `/{locale}/dev/gallery`. `ngDevMode` is
// replaced by `false` in optimized builds, so the route and its lazy chunk leave production.
const devRoutes: Routes =
  typeof ngDevMode === 'undefined' || ngDevMode
    ? [
        {
          path: 'dev/gallery',
          loadChildren: () => import('./ui/gallery/gallery.routes').then((m) => m.GALLERY_ROUTES),
        },
      ]
    : [];

// The store build of the apps shows no payment (ADR 0007): its donation URL is a 404. The
// lazy chunk is still emitted; only the route is withheld.
const paymentsOnly: CanMatchFn = () => inject(PAYMENTS_ENABLED);

const loadErrorPage = () => import('./features/errors/error-page').then((m) => m.ErrorPage);

// The page of an outcome (redirect, 404, failure) a resolver answered for the URL requested,
// rendered at that same URL (core/routing/outcome). First among its siblings, it matches the
// navigation carrying the outcome and no other. Its resolver reruns on every visit: two
// outcomes in a row share this route.
const outcomeRoute: Route = {
  path: '**',
  canMatch: [isOutcomeNavigation],
  runGuardsAndResolvers: 'always',
  resolve: { outcome: resolveOutcome },
  loadComponent: loadErrorPage,
};

// A URL no route knows: a real 404, rendered in place.
const notFoundRoute: Route = {
  path: '**',
  runGuardsAndResolvers: 'always',
  resolve: { outcome: resolveOutcome },
  loadComponent: loadErrorPage,
};

// The bare path of a mount without a page of its own. The router accepts a mount whose
// routes match nothing once the URL is consumed, and would render an empty page with a 200.
function bareNotFound(path: string): Route {
  return { ...notFoundRoute, path, pathMatch: 'full' };
}

// The four resources, mounted for the latest version and under a pinned `:version`. A
// function, so that each mount owns its route objects: the router caches lazy children on them.
function catalogueRoutes(): Routes {
  return [
    {
      path: 'champions',
      loadChildren: () =>
        import('./features/catalogue/champions/champions.routes').then((m) => m.CHAMPIONS_ROUTES),
    },
    {
      path: 'items',
      loadChildren: () =>
        import('./features/catalogue/items/items.routes').then((m) => m.ITEMS_ROUTES),
    },
    {
      path: 'runes',
      loadChildren: () =>
        import('./features/catalogue/runes/runes.routes').then((m) => m.RUNES_ROUTES),
    },
    {
      path: 'summoners',
      loadChildren: () =>
        import('./features/catalogue/summoners/summoners.routes').then((m) => m.SUMMONERS_ROUTES),
    },
  ];
}

// Sections with a path of their own come first. The mounts at the locale root (home,
// editorial pages) follow, so that no other page loads their chunks on the way, and precede
// `:version`, whose guard asks the API: a prerendered page never reaches it.
const localeRoutes: Routes = [
  outcomeRoute,
  bareNotFound('u'),
  bareNotFound('account'),
  ...catalogueRoutes(),
  {
    path: 'trends',
    loadChildren: () =>
      import('./features/builds/trends/trends.routes').then((m) => m.TRENDS_ROUTES),
  },
  {
    path: 'u',
    loadChildren: () => import('./features/profile/profile.routes').then((m) => m.PROFILE_ROUTES),
  },
  {
    path: 'developers',
    loadChildren: () =>
      import('./features/developers/developers.routes').then((m) => m.DEVELOPERS_ROUTES),
  },
  {
    path: 'donate',
    canMatch: [paymentsOnly],
    loadChildren: () => import('./features/donate/donate.routes').then((m) => m.DONATE_ROUTES),
  },
  {
    path: 'account/builds',
    loadChildren: () =>
      import('./features/builds/editor/editor.routes').then((m) => m.EDITOR_ROUTES),
  },
  {
    path: 'account/api',
    loadChildren: () =>
      import('./features/api-portal/api-portal.routes').then((m) => m.API_PORTAL_ROUTES),
  },
  {
    path: 'account',
    loadChildren: () => import('./features/account/account.routes').then((m) => m.ACCOUNT_ROUTES),
  },
  ...devRoutes,
  {
    path: '',
    loadChildren: () => import('./features/home/home.routes').then((m) => m.HOME_ROUTES),
  },
  {
    path: '',
    loadChildren: () =>
      import('./features/editorial/editorial.routes').then((m) => m.EDITORIAL_ROUTES),
  },
  // A pinned version only prefixes the catalogue: alone or before another page, a 404.
  {
    path: ':version',
    canMatch: [canMatchVersion],
    children: [...catalogueRoutes(), notFoundRoute],
  },
  notFoundRoute,
];

/**
 * The URL grammar of ADR 0005: every page under `/{locale}`, the catalogue also under a
 * pinned `/{locale}/{version}`, then the admin and the shared builds outside the locales.
 * Render modes and response headers are app.routes.server.ts's.
 */
export const routes: Routes = [
  // The SSR server answers `/` itself (src/server.ts); this redirect serves the shell build.
  { path: '', pathMatch: 'full', redirectTo: redirectToPreferredLocale },
  {
    path: ':locale',
    canMatch: [localeGuard],
    resolve: { locale: activateLocale },
    children: localeRoutes,
  },
  // Outside the locales, outcomes and missing pages speak the default locale.
  { ...outcomeRoute, resolve: { locale: activateLocale, outcome: resolveOutcome } },
  { ...bareNotFound('b'), resolve: { locale: activateLocale, outcome: resolveOutcome } },
  {
    path: 'admin',
    loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
  },
  {
    path: 'b',
    loadChildren: () => import('./features/builds/share/share.routes').then((m) => m.SHARE_ROUTES),
  },
  { ...notFoundRoute, resolve: { locale: activateLocale, outcome: resolveOutcome } },
];
