import type { Routes } from '@angular/router';
import { authenticatedGuard } from '../../core/auth/guards/authenticated-guard';
import type { AccountView } from './account-view';
import { anonymousGuard } from './shared/guards/anonymous-guard';
import { browserOnly } from './shared/guards/browser-only';

const loadPage = () => import('./account-page').then((m) => m.AccountPage);
const forAccounts = [browserOnly(authenticatedGuard)];
const forVisitors = [browserOnly(anonymousGuard)];

// `heading` is the translation key of the page's title, `view` what AccountPage shows.
function view(view: AccountView, heading: string) {
  return { data: { heading, view }, loadComponent: loadPage };
}

/**
 * The account pages, `/{locale}/account/...`: rendered in the browser only, `noindex` and
 * never cached (app.routes.server.ts). The builds (`account/builds`) and the API keys
 * (`account/api`) belong to their own features. The links of the e-mails land here:
 * `verify-email?user=&token=` and `reset-password/{token}?user=`.
 */
export const ACCOUNT_ROUTES: Routes = [
  { path: 'login', canActivate: forVisitors, ...view('login', 'auth.login.title') },
  { path: 'register', canActivate: forVisitors, ...view('register', 'auth.register.title') },
  { path: 'verify-email', ...view('verify-email', 'nav.account') },
  { path: 'forgot-password', ...view('forgot-password', 'auth.reset.request_title') },
  { path: 'reset-password/:token', ...view('reset-password', 'auth.reset.reset_title') },
  { path: 'profile', canActivate: forAccounts, ...view('profile', 'nav.profile') },
  {
    path: 'profile/preview',
    canActivate: forAccounts,
    ...view('preview', 'profile.preview.badge'),
  },
];
