import type { Routes } from '@angular/router';
import { authenticatedGuard } from '../../core/auth/guards/authenticated-guard';
import { browserOnly } from './shared/browser-only';

/**
 * The API key of an account, `/{locale}/account/api`, rendered in the browser only, never
 * cached nor indexed (app.routes.server.ts). A visitor goes to the login page, which brings
 * them back; Stripe comes back here with `?status=`.
 */
export const API_PORTAL_ROUTES: Routes = [
  {
    path: '',
    canActivate: [browserOnly(authenticatedGuard)],
    loadComponent: () => import('./api-portal-page').then((m) => m.ApiPortalPage),
  },
];
