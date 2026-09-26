import type { Routes } from '@angular/router';
import { activateLocale } from './core/i18n/activate-locale';
import { DEFAULT_LOCALE } from './core/i18n/default-locale';
import { localeGuard } from './core/i18n/locale-guard';

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

// Provisional tree: L3.1 brings the URL grammar of ADR 0005 and the lazy feature routes.
export const routes: Routes = [
  // The SSR server answers `/` itself (src/server.ts); this redirect serves the shell build.
  { path: '', pathMatch: 'full', redirectTo: DEFAULT_LOCALE },
  {
    path: ':locale',
    canMatch: [localeGuard],
    resolve: { locale: activateLocale },
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./hello/hello-page').then((m) => m.HelloPage),
      },
      ...devRoutes,
    ],
  },
];
