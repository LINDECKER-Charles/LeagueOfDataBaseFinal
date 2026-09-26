import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { DestroyRef, Injectable, Injector, PLATFORM_ID, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { PLATFORM } from '../platform/platform';
import { withHomeSlash } from '../routing/locale/with-home-slash';
import { pageKeyOf } from './page-key-of';

const VIEW_PATH = '/api/analytics/view';
const FRAGMENT_START = '#';

function withoutFragment(url: string): string {
  const start = url.indexOf(FRAGMENT_START);
  return start === -1 ? url : url.slice(0, start);
}

/**
 * Counts the navigations inside the application, which nginx never sees: at the end of each
 * one that changes the page (its path, language or version), the browser posts the page's
 * address with `navigator.sendBeacon`, which survives the page being left and costs the
 * navigation nothing. The first navigation is the page nginx served, already counted by its
 * mirror.
 *
 * Web only: the desktop and Android shells serve their pages themselves, and the server
 * renders no navigation of its own. Without `sendBeacon`, nothing is sent.
 */
@Injectable({ providedIn: 'root' })
export class NavigationBeacon {
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private counted: string | null = null;

  /** Listens to the router from now on; called once, at startup. */
  start(): void {
    if (!this.isBrowser) {
      return;
    }
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((event) => this.ended(withoutFragment(event.urlAfterRedirects)));
  }

  private ended(url: string): void {
    const page = pageKeyOf(url);
    const previous = this.counted;
    this.counted = page;
    if (previous !== null && previous !== page) {
      this.send(url);
    }
  }

  // The platform is read here, long after its detection has completed.
  private send(url: string): void {
    const platform = this.injector.get(PLATFORM);
    const navigator = this.document.defaultView?.navigator;
    if (platform.kind !== 'web' || typeof navigator?.sendBeacon !== 'function') {
      return;
    }
    const body = JSON.stringify({ path: withHomeSlash(url) });
    navigator.sendBeacon(`${platform.apiOrigin()}${VIEW_PATH}`, body);
  }
}
