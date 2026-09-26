import type { Routes } from '@angular/router';

const loadPage = () => import('./account-page').then((m) => m.AccountPage);

/**
 * The account pages, `/{locale}/account/...`: rendered in the browser only, `noindex` and
 * never cached (app.routes.server.ts). The builds (`account/builds`) and the API keys
 * (`account/api`) belong to their own features.
 */
export const ACCOUNT_ROUTES: Routes = [
  { path: 'login', data: { heading: 'auth.login.title' }, loadComponent: loadPage },
  { path: 'register', data: { heading: 'auth.register.title' }, loadComponent: loadPage },
  { path: 'verify-email', data: { heading: 'nav.account' }, loadComponent: loadPage },
  {
    path: 'forgot-password',
    data: { heading: 'auth.reset.request_title' },
    loadComponent: loadPage,
  },
  {
    path: 'reset-password/:token',
    data: { heading: 'auth.reset.reset_title' },
    loadComponent: loadPage,
  },
  { path: 'profile', data: { heading: 'nav.profile' }, loadComponent: loadPage },
  { path: 'profile/preview', data: { heading: 'profile.preview.badge' }, loadComponent: loadPage },
];
