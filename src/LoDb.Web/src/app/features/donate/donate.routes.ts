import type { Routes } from '@angular/router';

/** The donation page, `/{locale}/donate`, rendered by the server. */
export const DONATE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./donate-page').then((m) => m.DonatePage),
  },
];
