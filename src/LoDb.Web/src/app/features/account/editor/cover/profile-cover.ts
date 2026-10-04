import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { FAVORITE_SLOT } from '../../../../core/api/generated/models/favorite-slot-array';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { Button } from '../../../../ui/controls/button';
import { Image } from '../../../../ui/media/image';
import { SupporterBadge } from '../../preview/card/supporter-badge';
import { accountMessage } from '../../shared/account-message';
import { initialsOf } from '../entries/initials-of';
import type { FavoriteChoices } from '../store/favorite-choices';

/**
 * The head of the editor: the summoner's name, its favorites drifting as orbs, and the ways
 * to the preview of its public card and out of the account. A supporter's seal follows the
 * name, as on its public card.
 */
@Component({
  selector: 'lodb-profile-cover',
  imports: [Button, Image, RouterLink, SupporterBadge, TranslocoPipe],
  templateUrl: './profile-cover.html',
  styleUrl: './cover.css',
  host: { class: 'profile-cover' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileCover {
  private readonly session = inject(AuthSession);
  private readonly router = inject(Router);
  private readonly page = inject(PageDirection);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  readonly name = input.required<string>();
  readonly favorites = input.required<FavoriteChoices>();

  /** The favorites the patch shows, the champion first and larger. */
  protected readonly orbs = computed(() =>
    FAVORITE_SLOT.map((slot) => ({ slot, ...this.favorites()[slot] }))
      .filter((orb) => orb.name !== null)
      .map((orb) => ({ ...orb, initials: initialsOf(orb.name ?? '') })),
  );
  protected readonly preview = computed(() =>
    localePath(this.page.locale(), 'account/profile/preview'),
  );
  protected readonly supporter = computed(() => this.session.user()?.isSupporter === true);
  protected readonly leaving = signal(false);

  protected async signOut(): Promise<void> {
    this.leaving.set(true);
    const locale = this.page.locale();
    try {
      await this.session.signOut();
      await this.router.navigateByUrl(localePath(locale, ''));
    } catch {
      this.toasts.show(
        'error',
        await accountMessage(this.transloco, locale, 'account.errors.sign_out'),
      );
    } finally {
      this.leaving.set(false);
    }
  }
}
