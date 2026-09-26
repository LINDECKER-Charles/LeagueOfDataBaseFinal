import type { Routes } from '@angular/router';
import { resolveHome } from './loading/resolve-home';

/**
 * The home page, `/{locale}/`. app.routes.ts mounts this file at the locale root, after the
 * named sections, so that no other page loads it on the way. `?version=` and `?lang=` choose
 * its context: the resolver reruns when they change.
 */
export const HOME_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    runGuardsAndResolvers: 'paramsOrQueryParamsChange',
    resolve: { home: resolveHome },
    loadComponent: () => import('./home-page').then((m) => m.HomePage),
  },
];
