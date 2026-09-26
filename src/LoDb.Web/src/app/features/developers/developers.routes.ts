import type { Routes } from '@angular/router';

/** The public API documentation, `/{locale}/developers`, rendered by the server. */
export const DEVELOPERS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./developers-page').then((m) => m.DevelopersPage),
  },
];
