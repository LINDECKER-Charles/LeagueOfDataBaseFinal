/**
 * Query parameter of the account pages that names the page to come back to, such as
 * `/fr/account/login?returnUrl=%2Ffr%2Faccount%2Fbuilds`. The guards write it; the login page
 * (L4.6) passes it to `AuthSession.signIn` as `SignInTarget.returnUrl`.
 */
export const RETURN_URL_PARAM = 'returnUrl';
