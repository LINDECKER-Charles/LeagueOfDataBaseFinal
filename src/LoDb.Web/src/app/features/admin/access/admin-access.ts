import type { AccountUser } from '../../../core/api/generated/models/account-user';

// The Identity role of the administrators, as `AccountUser.roles` lists it.
const ADMIN_ROLE = 'Admin';

/**
 * Where an account stands at the door of the admin:
 * - `sign-in`: nobody is signed in;
 * - `refused`: the account is not an administrator, and must not learn the admin is here;
 * - `second-factor`: an administrator with an authenticator, signed in without it;
 * - `enroll`: an administrator without an authenticator yet, who must enrol one;
 * - `open`: an administrator whose session was opened with a second factor (ADR 0009).
 */
export type AdminAccess = 'sign-in' | 'refused' | 'second-factor' | 'enroll' | 'open';

/** The standing of `user`, the API's `Admin` policy read ahead of any call. */
export function adminAccessOf(user: AccountUser | null): AdminAccess {
  if (user === null) {
    return 'sign-in';
  }
  if (!user.roles.includes(ADMIN_ROLE)) {
    return 'refused';
  }
  if (user.multiFactor) {
    return 'open';
  }
  return user.twoFactorEnabled ? 'second-factor' : 'enroll';
}
