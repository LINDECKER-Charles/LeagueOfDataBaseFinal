import { Location } from '@angular/common';
import { type Signal, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  type ActivatedRouteSnapshot,
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  PRIMARY_OUTLET,
  Router,
  RoutesRecognized,
} from '@angular/router';
import { filter, map } from 'rxjs';

/** Route data key: `{ chrome: 'bare' }` renders a section in its own frame, as the admin. */
const CHROME_KEY = 'chrome';
const BARE = 'bare';

function isBare(snapshot: ActivatedRouteSnapshot | null): boolean {
  for (let route = snapshot; route !== null; route = route.firstChild) {
    if (route.data[CHROME_KEY] === BARE) {
      return true;
    }
  }
  return false;
}

// Before the first navigation is recognized (a page rendered in the browser only): the top
// route the path starts with, read from the configuration, never from a copied path. The
// router parses the address, so a query or a fragment (`/admin?range=7`) is no segment.
function startsBare(router: Router, path: string): boolean {
  const first = router.parseUrl(path).root.children[PRIMARY_OUTLET]?.segments[0]?.path;
  return router.config.some((route) => route.data?.[CHROME_KEY] === BARE && route.path === first);
}

/**
 * Whether the current section renders without the site's chrome (header, banners, footer,
 * bottom bar) and in the default identity: a route declares it with `data: { chrome: 'bare' }`.
 * Known as soon as the navigation is recognized, so the chrome never flashes before it goes.
 */
export function injectBareChrome(): Signal<boolean> {
  const router = inject(Router);
  const initial = startsBare(router, inject(Location).path());
  return toSignal(
    router.events.pipe(
      filter(
        (event) =>
          event instanceof RoutesRecognized ||
          event instanceof NavigationEnd ||
          event instanceof NavigationCancel ||
          event instanceof NavigationError,
      ),
      // A recognized navigation tells in advance; one that ends, fails or gives way leaves
      // the router on the section actually shown.
      map((event) =>
        isBare(
          event instanceof RoutesRecognized ? event.state.root : router.routerState.snapshot.root,
        ),
      ),
    ),
    { initialValue: initial },
  );
}
