import type { Routes } from '@angular/router';

/** The API keys of an account, `/{locale}/account/api`, rendered in the browser only. */
export const API_PORTAL_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./api-portal-page').then((m) => m.ApiPortalPage),
  },
];
