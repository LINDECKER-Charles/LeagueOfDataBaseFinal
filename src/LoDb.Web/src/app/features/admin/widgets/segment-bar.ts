import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminTextPipe } from '../shared/admin-text-pipe';

/** A choice of a segmented bar: the value it writes, the keys of its label and its name. */
export interface Segment {
  /** The value of the parameter; '' removes it, the choice of everything. */
  readonly value: string;
  /** The key of the short label it shows: "30 j". */
  readonly label: string;
  /** The key of its accessible name when the label is short: "30 jours". */
  readonly name?: string;
}

// Spelled out whole for the Tailwind scanner: the legacy `.rangebar` links.
const LINK = 'px-3 py-[0.35rem] font-beaufort text-[0.7rem] tracking-[0.12em] uppercase';
const IDLE = `${LINK} text-text-muted transition-colors hover:text-gold-bright`;
const ACTIVE = `${LINK} bg-linear-to-b from-gold-light to-gold text-void`;

/**
 * One choice among a few, kept in a parameter of the URL, as the legacy segmented bar drew
 * them: the period of an analytics panel, the status of the messages. Choosing one goes back
 * to the first page.
 */
@Component({
  selector: 'lodb-segment-bar',
  imports: [RouterLink, AdminTextPipe],
  template: `
    <nav
      class="inline-flex flex-wrap gap-[0.3rem] border border-gold/28 p-1"
      [attr.aria-label]="label() | adminText"
    >
      @for (segment of segments(); track segment.value) {
        <a
          [class]="segment.value === current() ? active : idle"
          [routerLink]="[]"
          [queryParams]="queryOf(segment)"
          queryParamsHandling="merge"
          [attr.aria-label]="segment.name ? (segment.name | adminText) : null"
          [attr.aria-current]="segment.value === current() ? 'page' : null"
          >{{ segment.label | adminText }}</a
        >
      }
    </nav>
  `,
  host: { class: 'inline-block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SegmentBar {
  /** The parameter of the URL the bar writes. */
  readonly param = input.required<string>();
  readonly segments = input.required<readonly Segment[]>();
  /** The value in the URL, '' when it has none. */
  readonly current = input.required<string>();
  /** The key of the name of the bar: "Période". */
  readonly label = input.required<string>();

  protected readonly idle = IDLE;
  protected readonly active = ACTIVE;

  protected queryOf(segment: Segment): Record<string, string | null> {
    return { [this.param()]: segment.value || null, page: null };
  }
}
