import type { Routes } from '@angular/router';

/**
 * The home page, `/{locale}/`. app.routes.ts mounts this file at the locale root, after the
 * named sections, so that no other page loads it on the way.
 */
export const HOME_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home-page').then((m) => m.HomePage),
  },
];
