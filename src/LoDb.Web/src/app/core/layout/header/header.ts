import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { provideTranslocoScope, TranslocoPipe } from '@jsverse/transloco';
import { PAYMENTS_ENABLED } from '../../../../environments/payments-enabled';
import { Button } from '../../../ui/controls/button';
import { Icon } from '../../../ui/media/icon';
import { Logo } from '../../../ui/media/logo';
import { PageDirection } from '../direction/page-direction';
import { Disclosure } from '../disclosure/disclosure';
import { localePath } from '../shell/locale-path';
import { RELEASE_VERSION } from '../shell/release-version';
import { ThemePicker } from '../theme-picker/theme-picker';
import { CODEX_ENTRIES } from './codex-entries';

/**
 * Slim Hextech bar: brand and release chip, the primary navigation from md up (the bottom
 * bar carries it below), then the cluster that stays on every viewport: donate (unless the
 * build shows no payment, ADR 0007), the `account` slot, the theme picker and the `switcher`
 * slot. The brand shortens to its initials wherever the row is crowded: under 460px, and
 * from md to lg where the navigation joins it. Under 400px the release chip goes, so a 320px screen never scrolls sideways.
 * The developers entry is labelled from the `api` catalogue scope, loaded here since the
 * header sits outside the pages that provide it.
 */
@Component({
  selector: 'lodb-header',
  imports: [
    Button,
    Disclosure,
    Icon,
    Logo,
    RouterLink,
    RouterLinkActive,
    ThemePicker,
    TranslocoPipe,
  ],
  providers: [provideTranslocoScope('api')],
  templateUrl: './header.html',
  host: { class: 'relative z-30 block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Header {
  protected readonly codex = CODEX_ENTRIES;
  protected readonly version = inject(RELEASE_VERSION);
  protected readonly payments = inject(PAYMENTS_ENABLED);
  private readonly page = inject(PageDirection);

  protected link(path: string): string {
    return localePath(this.page.locale(), path);
  }
}
