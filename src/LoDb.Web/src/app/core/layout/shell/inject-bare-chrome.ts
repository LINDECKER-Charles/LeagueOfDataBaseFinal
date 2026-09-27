import { Location } from '@angular/common';
import { type Signal, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  type ActivatedRouteSnapshot,
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  Router,
  RoutesRecognized,
  type Routes,
} from '@angular/router';
import { filter, map } from 'rxjs';

/** Route data key: `{ chrome: 'bare' }` renders a section in its own frame, as the admin. */
const CHROME_KEY = 'chrome';
const BARE = 'bare';
const SEGMENT_SEPARATOR = '/';

function isBare(snapshot: ActivatedRouteSnapshot | null): boolean {
  for (let route = snapshot; route !== null; route = route.firstChild) {
    if (route.data[CHROME_KEY] === BARE) {
      return true;
    }
  }
  return false;
}

// Before the first navigation is recognized (a page rendered in the browser only): the top
// route the path starts with, read from the configuration, never from a copied path.
function startsBare(config: Routes, path: string): boolean {
  const first = path.split(SEGMENT_SEPARATOR).find((segment) => segment !== '');
  return config.some((route) => route.data?.[CHROME_KEY] === BARE && route.path === first);
}

/**
 * Whether the current section renders without the site's chrome (header, banners, footer,
 * bottom bar) and in the default identity: a route declares it with `data: { chrome: 'bare' }`.
 * Known as soon as the navigation is recognized, so the chrome never flashes before it goes.
 */
export function injectBareChrome(): Signal<boolean> {
  const router = inject(Router);
  const initial = startsBare(router.config, inject(Location).path());
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
