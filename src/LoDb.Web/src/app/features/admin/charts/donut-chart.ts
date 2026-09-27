import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { figure } from '../format/figure';
import { FigurePipe } from '../format/figure-pipe';
import { AdminTextPipe } from '../shared/admin-text-pipe';
import { Legend } from '../widgets/legend';
import { donutArcs } from './scale/donut-arcs';
import type { DonutSlice } from './scale/donut-slice';

// The hole of the ring (radius 54, stroke 20) is 88 units wide: a longer total is squeezed
// into it rather than run over the ring.
const TOTAL_FITS = 6;
const TOTAL_WIDTH = 80;

/**
 * A part-to-whole ring with its total in the middle: one arc per slice, each titled with its
 * name, its value and its share, which a pointer reads on hover, and the legend under it with
 * the count of each slice, as the legacy one wrote it. An empty whole says so instead of
 * drawing an empty ring.
 */
@Component({
  selector: 'lodb-donut-chart',
  imports: [FigurePipe, Legend, AdminTextPipe],
  template: `
    <svg
      viewBox="0 0 180 180"
      class="mx-auto block w-full max-w-44"
      role="img"
      [attr.aria-label]="label()"
    >
      @if (arcs().length === 0) {
        <text x="90" y="94" text-anchor="middle" class="donut-empty">
          {{ 'admin.chart.empty' | adminText }}
        </text>
      } @else {
        <circle cx="90" cy="90" r="54" fill="none" class="donut-track" stroke-width="20" />
        @for (arc of arcs(); track arc.name) {
          <circle
            cx="90"
            cy="90"
            r="54"
            fill="none"
            stroke-width="20"
            transform="rotate(-90 90 90)"
            [attr.stroke]="arc.color"
            [attr.stroke-dasharray]="arc.dasharray"
            [attr.stroke-dashoffset]="arc.dashoffset"
          >
            <title>{{ arc.name }} — {{ arc.value | figure }} ({{ arc.pct | figure: 'pct' }})</title>
          </circle>
        }
        <text
          x="90"
          y="88"
          text-anchor="middle"
          class="donut-total"
          lengthAdjust="spacingAndGlyphs"
          [attr.textLength]="total().length > totalFits ? totalWidth : null"
        >
          {{ total() }}
        </text>
        <text x="90" y="106" text-anchor="middle" class="donut-caption">{{ caption() }}</text>
      }
    </svg>
    @if (legend().length > 0) {
      <lodb-legend [items]="legend()" />
    }
  `,
  styles: `
    .donut-track {
      stroke: var(--color-track);
    }
    .donut-total {
      font-family: var(--font-beaufort);
      font-size: 1.35rem;
      fill: var(--color-gold-bright);
    }
    .donut-caption {
      font-family: var(--font-mono);
      font-size: 9px;
      letter-spacing: 0.14em;
      text-transform: uppercase;
      fill: var(--color-text-muted);
    }
    .donut-empty {
      font-family: var(--font-mono);
      font-size: 13px;
      fill: var(--color-text-dim);
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DonutChart {
  readonly slices = input.required<readonly DonutSlice[]>();
  /** The accessible name of the chart, translated. */
  readonly label = input.required<string>();
  /** The figure in the middle, written. */
  readonly total = input('');
  /** The word under it: views, keys. */
  readonly caption = input('');

  protected readonly totalFits = TOTAL_FITS;
  protected readonly totalWidth = TOTAL_WIDTH;
  protected readonly arcs = computed(() => donutArcs(this.slices()));
  protected readonly legend = computed(() =>
    this.arcs().map((arc) => ({
      label: arc.name,
      color: arc.color,
      value: figure(arc.value, 'compact'),
    })),
  );
}
