import { sessionGuard } from './session-guard';

/** A signed-in account; anyone else goes to the login page, which brings them back. */
export const authenticatedGuard = sessionGuard((user, deny) =>
  user === null ? deny.accountPage('login') : true,
);
