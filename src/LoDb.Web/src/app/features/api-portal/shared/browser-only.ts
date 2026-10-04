import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';

/**
 * Runs a session guard in the browser only. The portal renders in the browser alone
 * (app.routes.server.ts): a server never reads a session, so were it ever to walk the
 * route, it lets it through and leaves the decision to the browser. The account pages and
 * the builds have their copy.
 */
export function browserOnly(guard: CanActivateFn): CanActivateFn {
  return (route, state) => (isPlatformBrowser(inject(PLATFORM_ID)) ? guard(route, state) : true);
}
