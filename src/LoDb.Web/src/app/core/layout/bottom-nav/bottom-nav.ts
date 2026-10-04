import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Icon } from '../../../ui/media/icon';
import { injectChromeLinks } from '../nav/inject-chrome-links';
import { NavContext } from '../nav/nav-context';
import { BOTTOM_NAV_ENTRIES } from './bottom-nav-entries';

/**
 * Primary navigation under md: a fixed bar in the thumb zone, clear of the home indicator.
 * Its catalogue tabs keep the version and the variant of the page (`injectChromeLinks`).
 */
@Component({
  selector: 'lodb-bottom-nav',
  imports: [Icon, RouterLink, RouterLinkActive, TranslocoPipe],
  templateUrl: './bottom-nav.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BottomNav {
  protected readonly links = injectChromeLinks(BOTTOM_NAV_ENTRIES);
  protected readonly home = inject(NavContext).home;
}
