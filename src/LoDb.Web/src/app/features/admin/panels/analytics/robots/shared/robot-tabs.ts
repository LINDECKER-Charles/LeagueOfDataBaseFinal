import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';

/** A view of the robots: the overview of the period, or the journal of the hits. */
export type RobotView = 'overview' | 'journal';

const VIEWS: readonly { readonly view: RobotView; readonly path: string }[] = [
  { view: 'overview', path: '/admin/robots' },
  { view: 'journal', path: '/admin/robots/journal' },
];
// Spelled out whole for the Tailwind scanner: the segmented links of the admin.
const LINK = 'px-3 py-[0.35rem] font-beaufort text-[0.7rem] tracking-[0.12em] uppercase';
const IDLE = `${LINK} text-text-muted transition-colors hover:text-gold-bright`;
const ACTIVE = `${LINK} bg-linear-to-b from-gold-light to-gold text-void`;

/** The two views of the robots, as a segmented bar in the head of their pages. */
@Component({
  selector: 'lodb-robot-tabs',
  imports: [RouterLink, AdminTextPipe],
  template: `
    <nav
      class="inline-flex gap-[0.3rem] border border-gold/28 p-1"
      [attr.aria-label]="'admin.robots.views.label' | adminText"
    >
      @for (entry of views; track entry.view) {
        <a
          [class]="entry.view === current() ? active : idle"
          [routerLink]="entry.path"
          [attr.aria-current]="entry.view === current() ? 'page' : null"
          >{{ 'admin.robots.views.' + entry.view | adminText }}</a
        >
      }
    </nav>
  `,
  host: { class: 'inline-block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RobotTabs {
  readonly current = input.required<RobotView>();

  protected readonly views = VIEWS;
  protected readonly idle = IDLE;
  protected readonly active = ACTIVE;
}
