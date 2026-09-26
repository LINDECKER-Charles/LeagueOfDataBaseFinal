import type { Routes } from '@angular/router';

/**
 * The admin, `/admin/...`, outside the locales: rendered in the browser only, `noindex` and
 * never cached (app.routes.server.ts), in its own lazy chunk.
 */
export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./admin-page').then((m) => m.AdminPage),
  },
];
