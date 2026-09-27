import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthSession } from '../../core/auth/session/auth-session';
import { Button } from '../../ui/controls/button';
import { Skeleton } from '../../ui/surfaces/skeleton';
import { ADMIN_NAV } from './admin-nav';
import { OverviewPanel } from './panels/analytics/overview/overview-panel';
import { ADMIN_PATHS } from './shared/admin-paths';

/**
 * The shell of the admin, `/admin/...`: the navigation of the legacy sidebar and the panel
 * of the URL, each in its own chunk, loaded when opened. `/admin` itself shows the overview,
 * deferred too. Its guard lets in an administrator whose session was opened with a second
 * factor only (admin.routes.ts).
 */
@Component({
  selector: 'lodb-admin-page',
  imports: [
    Button,
    OverviewPanel,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    Skeleton,
    TranslocoPipe,
  ],
  templateUrl: './admin-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminPage {
  private readonly session = inject(AuthSession);
  private readonly router = inject(Router);

  protected readonly nav = ADMIN_NAV;
  protected readonly user = this.session.user;
  /** Whether a panel of the URL fills the outlet; the overview shows otherwise. */
  protected readonly panelOpen = signal(false);
  protected readonly signingOut = signal(false);

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
