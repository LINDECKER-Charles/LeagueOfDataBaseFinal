import type { Routes } from '@angular/router';

/**
 * A shared build, `/b/{token}`, outside the locales: rendered by the server, always
 * `noindex` (app.routes.server.ts).
 */
export const SHARE_ROUTES: Routes = [
  {
    path: ':token',
    loadComponent: () => import('./share-page').then((m) => m.SharePage),
  },
];
