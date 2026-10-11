import { isPlatformBrowser } from '@angular/common';
import {
  ApplicationRef,
  DOCUMENT,
  DestroyRef,
  Injectable,
  PLATFORM_ID,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import type { AudienceData } from './audience/audience-data';
import type { AudienceEventName } from './audience/audience-event-name';
import type { AudiencePage } from './audience/audience-page';
import { audiencePageOf } from './audience/normalize/audience-page-of';
import { audienceReferrerOf } from './audience/normalize/audience-referrer-of';
import { AudienceSender } from './audience/send/audience-sender';

/**
 * The audience of the site, measured first-party (Umami behind the site's own `/u/`): a page
 * view at the end of every navigation that changes the page (path, language or version,
 * `pageKeyOf`), the first one included; a `page-error` instead when an outcome renders in
 * place of the page; a `search` when a list's search starts. Filters and paging rewrite the
 * query without a view. Never Umami's own script nor its automatic tracking.
 *
 * A view waits for the application to settle, so that its title is the page's own
 * (`Seo.apply`). Addresses are normalized before anything leaves (`audiencePageOf`); the
 * referrer is the referring site's origin, on the first view only. Browser only.
 */
@Injectable({ providedIn: 'root' })
export class AudienceTracker {
  private readonly router = inject(Router);
  private readonly appRef = inject(ApplicationRef);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly sender = inject(AudienceSender);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private current: AudiencePage | null = null;
  private lastKey: string | null = null;
  private wasSearching = false;
  private navigations = 0;

  /** Listens to the router from now on; called once, at startup, before the first navigation. */
  start(): void {
    if (!this.isBrowser) {
      return;
    }
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((event) => this.onNavigationEnd(event.urlAfterRedirects));
  }

  /** Counts an event of the page shown now (`build-share`); nothing on an unmeasured page. */
  track(name: AudienceEventName, data?: AudienceData): void {
    if (this.current !== null) {
      this.sender.send({ page: this.current, title: this.current.url, referrer: '', name, data });
    }
  }

  private onNavigationEnd(url: string): void {
    const page = audiencePageOf(this.router.routerState.snapshot.root, url);
    const isFirst = this.navigations === 0;
    this.navigations += 1;
    const key = page?.key ?? null;
    const isSamePage = key !== null && key === this.lastKey;
    this.lastKey = key;
    this.current = page;
    if (page === null || isSamePage) {
      this.searchStarted(page);
      return;
    }
    this.wasSearching = page.isSearching;
    if (page.errorStatus !== null) {
      this.track('page-error', { status: page.errorStatus });
      return;
    }
    void this.view(page, isFirst ? this.referrer() : '');
  }

  private searchStarted(page: AudiencePage | null): void {
    const isSearching = page?.isSearching ?? false;
    if (page !== null && isSearching && !this.wasSearching) {
      this.track('search');
    }
    this.wasSearching = isSearching;
  }

  // The page's head is written once its texts are loaded: its title is read once stable,
  // unless another navigation has ended meanwhile, whose title it would then be.
  private async view(page: AudiencePage, referrer: string): Promise<void> {
    const navigation = this.navigations;
    await this.appRef.whenStable();
    const isStill = navigation === this.navigations;
    const title = page.isPublicPath && isStill ? this.document.title : page.url;
    this.sender.send({ page, title, referrer });
  }

  private referrer(): string {
    return audienceReferrerOf(this.document.referrer, this.document.location?.hostname ?? '');
  }
}
