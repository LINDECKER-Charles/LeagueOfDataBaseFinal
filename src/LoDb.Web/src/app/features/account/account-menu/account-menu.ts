import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { Disclosure } from '../../../core/layout/disclosure/disclosure';
import { localePath } from '../../../core/layout/shell/locale-path';
import { ToastService } from '../../../core/layout/toast/toast-service';
import { Icon } from '../../../ui/media/icon';
import { accountMessage } from '../shared/account-message';
import { injectAuthSession } from '../shared/inject-auth-session';
import { shortName } from './short-name';

/**
 * The account menu of the header (`account` slot), a native `<details>`: the profile, the
 * builds and the sign-out for a signed-in account, the sign-in and the registration for
 * anyone else. The server renders it for a visitor: the session is read in the browser.
 */
@Component({
  selector: 'lodb-account-menu',
  imports: [Disclosure, Icon, RouterLink, RouterLinkActive, TranslocoPipe],
  templateUrl: './account-menu.html',
  host: { class: 'block shrink-0' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountMenu {
  private readonly session = injectAuthSession();
  private readonly router = inject(Router);
  private readonly page = inject(PageDirection);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  protected readonly user = computed(() => this.session?.user() ?? null);
  protected readonly label = computed(() => {
    const user = this.user();
    return user === null ? null : shortName(user.username);
  });
  protected readonly leaving = signal(false);
  protected readonly link = (path: string) => localePath(this.page.locale(), path);

  protected async signOut(): Promise<void> {
    this.leaving.set(true);
    try {
      await this.session?.signOut();
      await this.router.navigateByUrl(this.link(''));
    } catch {
      const locale = this.page.locale();
      const message = await accountMessage(this.transloco, locale, 'account.errors.sign_out');
      this.toasts.show('error', message);
    } finally {
      this.leaving.set(false);
    }
  }
}
