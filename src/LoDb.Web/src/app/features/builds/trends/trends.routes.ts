import type { Routes } from '@angular/router';

/** The trending builds, `/{locale}/trends`, rendered by the server. */
export const TRENDS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./trends-page').then((m) => m.TrendsPage),
  },
];
