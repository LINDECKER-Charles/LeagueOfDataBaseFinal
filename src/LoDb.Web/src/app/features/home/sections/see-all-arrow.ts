import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Arrow of a "see all" link, pointing forward: mirrored in right-to-left pages. */
@Component({
  selector: 'lodb-see-all-arrow',
  template: `<svg
    class="block size-full rtl:-scale-x-100"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="2"
  >
    <path d="M5 12h14M13 6l6 6-6 6" />
  </svg>`,
  host: { class: 'inline-block size-3.5 shrink-0', 'aria-hidden': 'true' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeeAllArrow {}
