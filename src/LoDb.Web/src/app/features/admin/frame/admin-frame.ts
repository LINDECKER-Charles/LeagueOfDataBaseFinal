import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ViewEncapsulation,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { AuthSession } from '../../../core/auth/session/auth-session';
import { adminAccessOf } from '../access/admin-access';
import { ADMIN_I18N } from '../shared/admin-i18n';
import { ADMIN_PATHS } from '../shared/admin-paths';
import { AdminTextPipe } from '../shared/admin-text-pipe';
import { ADMIN_NAV } from './admin-nav';

type NavLink = (typeof ADMIN_NAV)[number]['links'][number];

// What follows the path of a URL: the query and the fragment.
const AFTER_PATH = /[?#].*$/;

/**
 * The frame of every admin page, `/admin/...`, as the legacy back office drew it: its own top
 * bar (the brand, then the navigation and the sign-out once an administrator is in), and one
 * column under it. The admin speaks French: the document says so, and the writing direction
 * follows (PageDirection reads `<html lang>` after each navigation). Leaving the admin for a
 * page of the site, the locale resolver writes the language of its URL again.
 */
@Component({
  selector: 'lodb-admin-frame',
  imports: [RouterLink, RouterOutlet, AdminTextPipe],
  templateUrl: './admin-frame.html',
  styleUrl: './admin-frame.css',
  // Its sheet sizes the buttons of the pages it frames: scoped under its host by hand.
  encapsulation: ViewEncapsulation.None,
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
  private readonly path = computed(() => this.url().replace(AFTER_PATH, ''));

  protected readonly nav = ADMIN_NAV;
  /** The navigation shows to an administrator who passed the second factor only. */
  protected readonly open = computed(() => adminAccessOf(this.session.user()) === 'open');
  protected readonly signingOut = signal(false);

  constructor() {
    // Before the navigation ends, so that the direction read then is the French one.
    inject(DOCUMENT).documentElement.lang = ADMIN_I18N.lang;
  }

  protected isCurrent(link: NavLink): boolean {
    return link.current.test(this.path());
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
