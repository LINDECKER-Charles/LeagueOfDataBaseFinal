import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AdminTextPipe } from '../shared/admin-text-pipe';
import type { NavPlace } from './admin-nav';

/**
 * The bar over every admin page. Once an administrator is in, it says where the page stands
 * (its section, then its link) and, on a narrow screen, holds the button of the drawer;
 * before, it shows the brand alone.
 */
@Component({
  selector: 'lodb-admin-topbar',
  imports: [AdminTextPipe],
  template: `
    <header class="admin-bar">
      @if (open()) {
        <button
          type="button"
          class="admin-menu"
          aria-controls="admin-sidebar"
          [attr.aria-expanded]="drawerOpen()"
          [attr.aria-label]="'admin.nav.' + (drawerOpen() ? 'menu_close' : 'menu_open') | adminText"
          (click)="menuToggle.emit()"
        >
          <span aria-hidden="true"></span>
        </button>
        @if (place(); as here) {
          <p class="admin-crumbs">
            <span>{{ 'admin.nav.' + here.section.key | adminText }}</span>
            @if (here.section.links.length > 1) {
              <span class="admin-crumbs-sep" aria-hidden="true"></span>
              <b>{{ 'admin.nav.' + here.link.key | adminText }}</b>
            }
          </p>
        }
      } @else {
        <span class="admin-brand">
          <b>{{ 'admin.brand_name' | adminText }}</b>
          <span>{{ 'admin.brand_tag' | adminText }}</span>
        </span>
      }
    </header>
  `,
  styleUrl: './admin-topbar.css',
  host: { class: 'admin-topbar' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminTopbar {
  /** Whether an administrator passed the second factor: the navigation shows. */
  readonly open = input(false);
  readonly place = input<NavPlace | null>(null);
  readonly drawerOpen = input(false);
  readonly menuToggle = output();
}
