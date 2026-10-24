import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ViewEncapsulation,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { AuthSession } from '../../../core/auth/session/auth-session';
import { adminAccessOf } from '../access/admin-access';
import { ADMIN_I18N } from '../shared/admin-i18n';
import { ADMIN_PATHS } from '../shared/admin-paths';
import { AdminTextPipe } from '../shared/admin-text-pipe';
import { navPlaceOf } from './admin-nav';
import { AdminSidebar } from './admin-sidebar';
import { AdminTopbar } from './admin-topbar';

// What follows the path of a URL: the query and the fragment.
const AFTER_PATH = /[?#].*$/;
const ESCAPE = 'Escape';

/**
 * The frame of every admin page, `/admin/...`: once an administrator is in, the sidebar of
 * the four sections (Dashboard, Analytics, Gestion, Health) beside the page and the bar over
 * it; before, the bar shows the brand alone over one column. On a narrow screen the sidebar
 * is a drawer the bar opens, closed again by a navigation, its backdrop or Escape. The admin
 * speaks French: the document says so, and the writing direction follows (PageDirection reads
 * `<html lang>` after each navigation). Leaving the admin for a page of the site, the locale
 * resolver writes the language of its URL again.
 */
@Component({
  selector: 'lodb-admin-frame',
  imports: [AdminSidebar, AdminTopbar, RouterOutlet, AdminTextPipe],
  template: `
    @if (open()) {
      <lodb-admin-sidebar
        [path]="path()"
        [drawerOpen]="drawerOpen()"
        [username]="username()"
        [signingOut]="signingOut()"
        (signOut)="signOut()"
      />
      @if (drawerOpen()) {
        <button
          type="button"
          class="admin-backdrop"
          [attr.aria-label]="'admin.nav.menu_close' | adminText"
          (click)="toggleDrawer()"
        ></button>
      }
    }
    <div class="admin-main">
      <lodb-admin-topbar
        [open]="open()"
        [place]="place()"
        [drawerOpen]="drawerOpen()"
        (menuToggle)="toggleDrawer()"
      />
      <div class="admin-column">
        <router-outlet />
      </div>
    </div>
  `,
  styleUrl: './admin-frame.css',
  // Its sheet sizes the buttons of the pages it frames: scoped under its host by hand.
  encapsulation: ViewEncapsulation.None,
  host: { '[class.is-open]': 'open()', '(document:keydown)': 'onKeydown($event)' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminFrame {
  private readonly session = inject(AuthSession);
  private readonly router = inject(Router);
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  protected readonly path = computed(() => this.url().replace(AFTER_PATH, ''));
  /** The navigation shows to an administrator who passed the second factor only. */
  protected readonly open = computed(() => adminAccessOf(this.session.user()) === 'open');
  protected readonly place = computed(() => navPlaceOf(this.path()));
  protected readonly username = computed(() => this.session.user()?.username ?? '');
  protected readonly drawerOpen = signal(false);
  protected readonly signingOut = signal(false);

  constructor() {
    // Before the navigation ends, so that the direction read then is the French one.
    inject(DOCUMENT).documentElement.lang = ADMIN_I18N.lang;
    // A page opened from the drawer closes it.
    effect(() => {
      this.path();
      this.drawerOpen.set(false);
    });
  }

  protected toggleDrawer(): void {
    this.drawerOpen.update((open) => !open);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === ESCAPE && this.drawerOpen()) {
      this.drawerOpen.set(false);
    }
  }

  protected async signOut(): Promise<void> {
    this.signingOut.set(true);
    try {
      await this.session.signOut();
      await this.router.navigateByUrl(ADMIN_PATHS.login);
    } finally {
      this.signingOut.set(false);
    }
  }
}
