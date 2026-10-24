import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { NavIconName } from './admin-nav';

/**
 * The glyph of a section of the sidebar, on the icon grammar of the site (24px box,
 * currentColor stroke): a hextech tile for the dashboard, bars for the analytics, a
 * shielded figure for the management, a pulse for the health. Always decorative.
 */
@Component({
  selector: 'lodb-nav-icon',
  template: `
    <svg
      class="block size-full"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.5"
      stroke-linecap="round"
      stroke-linejoin="round"
    >
      @switch (name()) {
        @case ('dashboard') {
          <path d="M12 2.8 20 7.4v9.2L12 21.2 4 16.6V7.4z" />
          <path d="M12 7.6 16 9.9v4.6l-4 2.3-4-2.3V9.9z" />
        }
        @case ('analytics') {
          <path d="M4 20h16" />
          <path d="M7 16v-5M12 16V6M17 16v-8" />
        }
        @case ('management') {
          <path d="M12 3 19 6v5.5c0 4.2-3 7.6-7 9.5-4-1.9-7-5.3-7-9.5V6z" />
          <circle cx="12" cy="10" r="2.4" />
          <path d="M8.4 16c.8-1.7 2.1-2.6 3.6-2.6s2.8.9 3.6 2.6" />
        }
        @case ('health') {
          <path d="M3 12h4l2-5 4 10 2-5h6" />
        }
      }
    </svg>
  `,
  host: { class: 'inline-block size-[1.15rem] shrink-0', 'aria-hidden': 'true' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NavIcon {
  readonly name = input.required<NavIconName>();
}
