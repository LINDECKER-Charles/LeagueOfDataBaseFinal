import {
  type Event,
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  type Router,
} from '@angular/router';
import { filter, firstValueFrom } from 'rxjs';

function isNavigationOutcome(event: Event): boolean {
  return (
    event instanceof NavigationEnd ||
    event instanceof NavigationCancel ||
    event instanceof NavigationError
  );
}

/**
 * Resolves once the router has settled its first navigation. An App Link can start the app
 * cold: a navigation of its own made earlier would be overridden by the initial one.
 */
export async function afterFirstNavigation(router: Router): Promise<void> {
  if (router.navigated) {
    return;
  }
  await firstValueFrom(router.events.pipe(filter(isNavigationOutcome)));
}
