import type { Routes } from '@angular/router';
import { activateLocale } from './core/i18n/activate-locale';
import { DEFAULT_LOCALE } from './core/i18n/default-locale';
import { localeGuard } from './core/i18n/locale-guard';

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
    ],
  },
];
