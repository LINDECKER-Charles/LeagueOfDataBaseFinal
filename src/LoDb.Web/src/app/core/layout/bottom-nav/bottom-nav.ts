import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Icon } from '../../../ui/media/icon';
import { PageDirection } from '../direction/page-direction';
import { localePath } from '../shell/locale-path';
import { BOTTOM_NAV_ENTRIES } from './bottom-nav-entries';

/** Primary navigation under md: a fixed bar in the thumb zone, clear of the home indicator. */
@Component({
  selector: 'lodb-bottom-nav',
  imports: [Icon, RouterLink, RouterLinkActive, TranslocoPipe],
  templateUrl: './bottom-nav.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BottomNav {
  protected readonly entries = BOTTOM_NAV_ENTRIES;
  private readonly page = inject(PageDirection);

  protected link(path: string): string {
    return localePath(this.page.locale(), path);
  }
}
