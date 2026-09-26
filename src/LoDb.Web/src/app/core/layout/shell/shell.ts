import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ThemeService } from '../../theme/theme-service';
import { BottomNav } from '../bottom-nav/bottom-nav';
import { PageDirection } from '../direction/page-direction';
import { Footer } from '../footer/footer';
import { Header } from '../header/header';
import { Toaster } from '../toast/toaster';

/**
 * Page envelope with the projection slots of the plan (section 5.2): `[lodbSlot=switcher]`,
 * `[lodbSlot=account]`, `[lodbSlot=banner]`, `[lodbSlot=contact]`, and the page itself as
 * default content. The slots are a contract: L3.1 plugs provisional components into them and
 * their chantiers replace those components, while this envelope owns where they land.
 * Neither renames a slot.
 *
 * The header and the footer are components of their own, so each slot is projected twice:
 * `ngProjectAs` hands the caller's content on under the same selector.
 */
@Component({
  selector: 'lodb-shell',
  imports: [BottomNav, Footer, Header, Toaster],
  templateUrl: './shell.html',
  host: { class: 'hx-shell flex min-h-dvh flex-col' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {
  constructor() {
    // Both act on <html> from construction: the text direction of the locale, and the
    // browser chrome colour of the painted theme.
    inject(PageDirection);
    inject(ThemeService);
  }
}
