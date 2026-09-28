import type { Routes } from '@angular/router';
import { resolvePublicApiReference } from './reference/resolve-public-api-reference';

/** The public API documentation, `/{locale}/developers`, rendered by the server. */
export const DEVELOPERS_ROUTES: Routes = [
  {
    path: '',
    resolve: { reference: resolvePublicApiReference },
    loadComponent: () => import('./developers-page').then((m) => m.DevelopersPage),
  },
];
