import type { Routes } from '@angular/router';
import { provideTranslocoLang, provideTranslocoScope } from '@jsverse/transloco';
import { adminAccessGuard } from './access/admin-access-guard';
import { PANEL_ROUTES } from './panels/panel.routes';
import { ADMIN_I18N } from './shared/admin-i18n';

/**
 * The admin, `/admin/...`, outside the locales: rendered in the browser only, `noindex` and
 * never cached (app.routes.server.ts), in its own lazy chunk and in French. Its own login
 * page (password, then the authenticator), the enrolment of an authenticator, then the
 * panels, open to an administrator whose session was opened with a second factor only.
 */
export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    providers: [provideTranslocoScope(ADMIN_I18N.scope), provideTranslocoLang(ADMIN_I18N.lang)],
    children: [
      {
        path: 'login',
        canActivate: [adminAccessGuard('sign-in', 'second-factor', 'refused')],
        loadComponent: () => import('./sign-in/admin-login-page').then((m) => m.AdminLoginPage),
      },
      {
        path: 'enroll',
        canActivate: [adminAccessGuard('enroll')],
        loadComponent: () => import('./enroll/admin-enroll-page').then((m) => m.AdminEnrollPage),
      },
      {
        path: '',
        canActivate: [adminAccessGuard('open')],
        canActivateChild: [adminAccessGuard('open')],
        loadComponent: () => import('./admin-page').then((m) => m.AdminPage),
        children: PANEL_ROUTES,
      },
    ],
  },
];
