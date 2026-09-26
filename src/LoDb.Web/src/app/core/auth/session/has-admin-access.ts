import type { AccountUser } from '../../api/generated/models/account-user';

// The Identity role of the administrators, as `AccountUser.roles` lists it.
const ADMIN_ROLE = 'Admin';

/**
 * Whether the account may open the admin: the `Admin` role and a session opened with a
 * second factor, as the API's `Admin` policy requires (ADR 0009). The API decides anyway;
 * this only spares a page that would answer 403 everywhere.
 */
export function hasAdminAccess(user: AccountUser | null): boolean {
  return user !== null && user.multiFactor && user.roles.includes(ADMIN_ROLE);
}
