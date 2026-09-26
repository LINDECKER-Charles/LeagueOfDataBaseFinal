import { sessionGuard } from './session-guard';

/**
 * A signed-in account whose e-mail is verified, which creating a public build or an API key
 * requires (the API's `VerifiedEmail` policy). An unverified account goes to the
 * verification page, which offers to send the link again and brings it back.
 */
export const verifiedEmailGuard = sessionGuard((user, deny) => {
  if (user === null) {
    return deny.accountPage('login');
  }
  return user.emailVerified ? true : deny.accountPage('verify-email');
});
