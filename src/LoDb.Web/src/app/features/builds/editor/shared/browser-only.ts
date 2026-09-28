import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';

/**
 * Runs a session guard in the browser only. The account's builds render in the browser
 * alone (app.routes.server.ts): a server never reads a session, so were it ever to walk
 * these routes, it lets them through and leaves the decision to the browser. The account
 * pages have their copy.
 */
export function browserOnly(guard: CanActivateFn): CanActivateFn {
  return (route, state) => (isPlatformBrowser(inject(PLATFORM_ID)) ? guard(route, state) : true);
}
