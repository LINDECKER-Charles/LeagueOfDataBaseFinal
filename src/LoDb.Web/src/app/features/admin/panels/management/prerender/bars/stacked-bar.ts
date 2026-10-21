import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { stackedSegments } from './stacked-segments';

/** A segment of a stacked bar. */
export interface BarSegment {
  readonly key: string;
  readonly value: number;
  /** A CSS colour, a token. */
  readonly color: string;
  /** Translated, for the legend and the accessible name. */
  readonly label: string;
}

const PERCENT = 100;

/**
 * A bar split into segments, side by side from the start of the line (the end in a
 * right-to-left locale), over the track: pages written, skipped, failed and left, say. A
 * segment that is not nothing keeps two pixels at least. One image to assistive technology,
 * named by `label`; the legend beside it says the values. Muted, desaturated: a paused pass.
 */
@Component({
  selector: 'lodb-stacked-bar',
  template: `
    <div
      class="bar"
      role="img"
      [attr.aria-label]="label()"
      [attr.data-size]="size()"
      [class.bar--muted]="isMuted()"
    >
      @for (segment of shares(); track segment.key) {
        @if (segment.share > 0) {
          <span
            class="bar__segment"
            [attr.data-segment]="segment.key"
            [style.inline-size.%]="segment.share * percent"
            [style.background]="segment.color"
          ></span>
        }
      }
    </div>
  `,
  styles: `
    .bar {
      display: flex;
      overflow: hidden;
      block-size: 0.5rem;
      background: var(--color-track);
    }
    .bar[data-size='thin'] {
      block-size: 0.25rem;
    }
    .bar[data-size='hero'] {
      block-size: 0.875rem;
    }
    .bar--muted {
      filter: saturate(0.35);
    }
    .bar__segment {
      flex: none;
      min-inline-size: 2px;
      transition: inline-size 0.4s var(--ease-hextech);
    }
    @media (prefers-reduced-motion: reduce) {
      .bar__segment {
        transition: none;
      }
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StackedBar {
  readonly segments = input.required<readonly BarSegment[]>();
  /** The whole bar's accessible name, translated. */
  readonly label = input.required<string>();
  readonly size = input<'thin' | 'regular' | 'hero'>('regular'); // 4, 8, 14 px
  readonly isMuted = input(false);

  protected readonly shares = computed(() => stackedSegments(this.segments()));
  protected readonly percent = PERCENT;
}
