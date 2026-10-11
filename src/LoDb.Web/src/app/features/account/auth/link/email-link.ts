import type { ActivatedRouteSnapshot } from '@angular/router';

const USER_PARAM = 'user';
const TOKEN_PARAM = 'token';

/** The account and the one-time token the link of an account e-mail carries. */
export interface EmailLink {
  readonly user: string;
  readonly token: string;
}

/**
 * The link of an account e-mail the page was opened with. Its token sits in the fragment
 * (`?user={id}#token={token}`), which the browser never sends: neither a server, a log nor
 * a referrer sees it. The links sent before held it in the query (`verify-email?token=`) or
 * the path (`reset-password/{token}`): they are still read while they may be valid.
 */
export function emailLinkOf(route: ActivatedRouteSnapshot): EmailLink {
  const token =
    new URLSearchParams(route.fragment ?? '').get(TOKEN_PARAM) ??
    route.queryParamMap.get(TOKEN_PARAM) ??
    route.paramMap.get(TOKEN_PARAM) ??
    '';
  return { user: route.queryParamMap.get(USER_PARAM) ?? '', token };
}
