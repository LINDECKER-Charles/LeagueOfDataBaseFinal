import { hasAdminAccess } from '../session/has-admin-access';
import { sessionGuard } from './session-guard';

/**
 * An administrator whose session was opened with a second factor (the API's `Admin`
 * policy). Anonymous visitors go to the login page; any other account gets the 404 of the
 * URL, which does not tell that an admin lives there.
 */
export const adminGuard = sessionGuard((user, deny) => {
  if (user === null) {
    return deny.accountPage('login');
  }
  return hasAdminAccess(user) ? true : deny.notFound();
});
