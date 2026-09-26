import { isPlatformBrowser } from '@angular/common';
import { DestroyRef, Injectable, PLATFORM_ID, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import type { FilterUrlBinding } from './filter-url-binding';
import type { FilterUrlSpec } from '../filter-url-spec';
import type { FilterUrlState } from '../filter-url-state';
import { parseFilterUrl } from '../parse-filter-url';
import { pathOf } from '../path-of';
import { searchOf } from '../search-of';
import { TrailingThrottle } from './trailing-throttle';
import { writeFilterUrl } from '../write-filter-url';

/**
 * Safari refuses more than 100 history rewrites per 30 s, and a slider drag alone would
 * exceed it: rewrites coalesce over this window.
 */
const WRITE_WINDOW_MS = 300;
// Before any list is bound, a URL keeps its query as is.
const EMPTY_SPEC: FilterUrlSpec = { schema: [], defaultSize: 0 };

/**
 * Keeps the URL of a list equal to its state, so a filtered list is a link. The state is
 * the truth: the URL follows it through the router (replacing the entry, never adding one),
 * coalesced over a short window. A navigation the list did not ask for, such as a link to
 * the bare list, hands its state back. Nothing is written while rendering on the server.
 */
@Injectable()
export class FilterUrlSync {
  private readonly router = inject(Router);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly destroyRef = inject(DestroyRef);
  private readonly throttle = new TrailingThrottle(WRITE_WINDOW_MS, () => this.write());
  private binding: FilterUrlBinding | null = null;
  private requested: string | null = null;

  constructor() {
    this.destroyRef.onDestroy(() => this.throttle.cancel());
  }

  /** The query string of the current URL, `?` included. */
  search(): string {
    return searchOf(this.router.url);
  }

  /** Binds the list, then follows the navigations it did not ask for. */
  bind(binding: FilterUrlBinding): void {
    this.binding = binding;
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.follow());
  }

  /** Asks for the URL to catch up with the state; coalesced. */
  request(): void {
    if (this.isBrowser && this.binding !== null) {
      this.throttle.request();
    }
  }

  /** The URL of the list in another state: a page link, a shared link. */
  urlOf(state: FilterUrlState): string {
    const url = this.router.url;
    return pathOf(url) + writeFilterUrl(searchOf(url), state, this.binding?.spec() ?? EMPTY_SPEC);
  }

  private write(): void {
    const binding = this.binding;
    if (binding === null) {
      return;
    }
    const target = this.urlOf(binding.current());
    const canonical = this.router.serializeUrl(this.router.parseUrl(target));
    if (canonical === this.router.url) {
      return;
    }
    this.requested = canonical;
    void this.router.navigateByUrl(target, { replaceUrl: true });
  }

  private follow(): void {
    const binding = this.binding;
    const url = this.router.url;
    if (binding === null || url === this.requested) {
      return;
    }
    this.requested = null;
    binding.adopt(parseFilterUrl(searchOf(url), binding.spec()));
  }
}
