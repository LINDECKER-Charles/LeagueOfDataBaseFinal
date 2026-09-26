import { DOCUMENT, inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';
import { RETURN_URL_PARAM } from '../../../../core/auth/guards/return-url-param';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import { DEFAULT_LOCALE } from '../../../../core/i18n/default-locale';
import { isLocale } from '../../../../core/i18n/is-locale';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { safeReturnUrl } from '../../../../core/routing/safe-return-url';

/**
 * Sign-in and registration are for visitors: a signed-in account goes on to the page it was
 * asked to return to, or to its profile. A session that cannot be read shows the form, since
 * signing in reads it again.
 */
export const anonymousGuard: CanActivateFn = (route) => {
  const session = inject(AuthSession);
  const router = inject(Router);
  const origin = inject(DOCUMENT).location.origin;
  const requested = route.paramMap.get('locale');
  const profile = localePath(isLocale(requested) ? requested : DEFAULT_LOCALE, 'account/profile');
  const returnUrl = route.queryParamMap.get(RETURN_URL_PARAM);
  return session.load().then(
    (user) => user === null || router.parseUrl(safeReturnUrl(returnUrl, origin, profile)),
    () => true,
  );
};
