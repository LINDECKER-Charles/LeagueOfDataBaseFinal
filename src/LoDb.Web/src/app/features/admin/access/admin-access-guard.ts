import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, inject } from '@angular/core';
import {
  type CanActivateFn,
  type GuardResult,
  Router,
  type RouterStateSnapshot,
} from '@angular/router';
import { RETURN_URL_PARAM } from '../../../core/auth/guards/return-url-param';
import { AuthSession } from '../../../core/auth/session/auth-session';
import { failureOf } from '../../../core/routing/failure/failure-of';
import {
  type OutcomeCommand,
  injectOutcomeCommand,
} from '../../../core/routing/outcome/inject-outcome-command';
import { NOT_FOUND } from '../../../core/routing/outcome/not-found';
import { ADMIN_PATHS } from '../shared/admin-paths';
import { type AdminAccess, adminAccessOf } from './admin-access';

/**
 * Builds the guard of an admin page open to the accounts whose standing is in `allowed`.
 * Anyone else goes where their standing leads: the admin's own login page (which brings
 * them back), the enrolment of an authenticator, the admin itself, or the 404 of the URL for
 * an account that is no administrator, which does not tell an admin lives there. A session
 * that cannot be read renders the failure of the URL (503 when the API is down).
 *
 * The admin renders in the browser only (app.routes.server.ts): on the server, where no
 * session is ever read, the guard lets the shell through. Usable in `canActivate` and
 * `canActivateChild`.
 */
export function adminAccessGuard(...allowed: readonly AdminAccess[]): CanActivateFn {
  return (_route, state) => {
    if (!isPlatformBrowser(inject(PLATFORM_ID))) {
      return true;
    }
    const outcome = injectOutcomeCommand();
    const answer = injectAnswer(state, outcome);
    return inject(AuthSession)
      .load()
      .then(
        (user) => {
          const access = adminAccessOf(user);
          return allowed.includes(access) ? true : answer(access);
        },
        (error: unknown) => outcome(failureOf(error), state.url),
      );
  };
}

// What a refused standing gets instead of the page, the injections done up front.
function injectAnswer(
  state: RouterStateSnapshot,
  outcome: OutcomeCommand,
): (access: AdminAccess) => GuardResult {
  const router = inject(Router);
  const login = router.createUrlTree([ADMIN_PATHS.login], {
    queryParams: { [RETURN_URL_PARAM]: state.url },
  });
  const answers: Readonly<Record<AdminAccess, () => GuardResult>> = {
    'sign-in': () => login,
    'second-factor': () => login,
    enroll: () => router.parseUrl(ADMIN_PATHS.enroll),
    open: () => router.parseUrl(ADMIN_PATHS.home),
    refused: () => outcome(NOT_FOUND, state.url),
  };
  return (access) => answers[access]();
}
