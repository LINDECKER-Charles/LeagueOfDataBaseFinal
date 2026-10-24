import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminTextPipe } from '../shared/admin-text-pipe';
import { ADMIN_NAV, type NavLink, type NavSection, navPlaceOf } from './admin-nav';
import { NavIcon } from './nav-icon';

/**
 * The sidebar of the admin: the brand, the four sections and their links, the current page
 * marked, then the account and its sign-out. Fixed beside the page on a wide screen, a drawer
 * on a narrow one (`drawerOpen`).
 */
@Component({
  selector: 'lodb-admin-sidebar',
  imports: [NavIcon, RouterLink, AdminTextPipe],
  templateUrl: './admin-sidebar.html',
  styleUrl: './admin-sidebar.css',
  host: {
    id: 'admin-sidebar',
    class: 'admin-sidebar',
    '[class.is-drawer-open]': 'drawerOpen()',
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminSidebar {
  /** The path of the URL, query and fragment left out. */
  readonly path = input.required<string>();
  readonly drawerOpen = input(false);
  readonly username = input('');
  readonly signingOut = input(false);
  readonly signOut = output();

  protected readonly nav = ADMIN_NAV;
  private readonly place = computed(() => navPlaceOf(this.path()));

  protected isCurrent(link: NavLink): boolean {
    return link.current.test(this.path());
  }

  protected isCurrentSection(section: NavSection): boolean {
    return this.place()?.section === section;
  }
}
