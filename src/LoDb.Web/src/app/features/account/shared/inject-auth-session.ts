import { inject } from '@angular/core';
import { AuthSession } from '../../../core/auth/session/auth-session';
import { PLATFORM } from '../../../core/platform/platform';

/**
 * The session for the chrome of every page (header menu, banner), or null in an application
 * that never detected its platform, such as the shell's own specs: the session reads through
 * the platform's strategy, and the chrome then shows what a visitor sees, as on the server.
 */
export function injectAuthSession(): AuthSession | null {
  return inject(PLATFORM, { optional: true }) === null ? null : inject(AuthSession);
}
