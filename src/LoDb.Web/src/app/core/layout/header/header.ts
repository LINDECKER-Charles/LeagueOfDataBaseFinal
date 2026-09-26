import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
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
 * bar carries it below), then the cluster that stays on every viewport: donate, the
 * `account` slot, the theme picker and the `switcher` slot. Under 460px the brand shortens
 * to its initials and under 400px the release chip goes, so a 320px screen never scrolls
 * sideways.
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
  templateUrl: './header.html',
  host: { class: 'relative z-30 block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Header {
  protected readonly codex = CODEX_ENTRIES;
  protected readonly version = inject(RELEASE_VERSION);
  private readonly page = inject(PageDirection);

  protected link(path: string): string {
    return localePath(this.page.locale(), path);
  }
}
