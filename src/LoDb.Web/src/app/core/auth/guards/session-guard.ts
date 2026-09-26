import { inject } from '@angular/core';
import {
  type ActivatedRouteSnapshot,
  type CanActivateFn,
  type GuardResult,
  Router,
} from '@angular/router';
import type { AccountUser } from '../../api/generated/models/account-user';
import { DEFAULT_LOCALE } from '../../i18n/default-locale';
import { isLocale } from '../../i18n/is-locale';
import type { Locale } from '../../i18n/locales';
import { failureOf } from '../../routing/failure/failure-of';
import { injectOutcomeCommand } from '../../routing/outcome/inject-outcome-command';
import { NOT_FOUND } from '../../routing/outcome/not-found';
import { AuthSession } from '../session/auth-session';
import { RETURN_URL_PARAM } from './return-url-param';
import type { SessionDenials } from './session-denials';

// The admin lives outside the locales: its sign-in speaks the default one.
function localeOf(route: ActivatedRouteSnapshot): Locale {
  return route.pathFromRoot.map((step) => step.params['locale']).find(isLocale) ?? DEFAULT_LOCALE;
}

/**
 * Builds a guard that waits for the session, then lets `check` decide. A session that cannot
 * be read renders the failure of the requested URL (503 when the API is down), as a failed
 * resolver does. Usable in `canActivate` and `canActivateChild`.
 */
export function sessionGuard(
  check: (user: AccountUser | null, deny: SessionDenials) => GuardResult,
): CanActivateFn {
  return (route, state) => {
    const session = inject(AuthSession);
    const router = inject(Router);
    const outcome = injectOutcomeCommand();
    const deny: SessionDenials = {
      accountPage: (page) =>
        router.createUrlTree(['/', localeOf(route), 'account', page], {
          queryParams: { [RETURN_URL_PARAM]: state.url },
        }),
      notFound: () => outcome(NOT_FOUND, state.url),
    };
    return session.load().then(
      (user) => check(user, deny),
      (error: unknown) => outcome(failureOf(error), state.url),
    );
  };
}
