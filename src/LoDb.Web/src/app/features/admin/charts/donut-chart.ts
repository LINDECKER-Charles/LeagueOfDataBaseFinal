import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { figure } from '../format/figure';
import { FigurePipe } from '../format/figure-pipe';
import { Legend } from '../widgets/legend';
import { donutArcs } from './scale/donut-arcs';
import type { DonutSlice } from './scale/donut-slice';

/**
 * A part-to-whole ring with its total in the middle: one arc per slice, each titled with its
 * name, its value and its share, which a pointer reads on hover, and the same shares in the
 * legend under it. An empty whole says so instead of drawing an empty ring.
 */
@Component({
  selector: 'lodb-donut-chart',
  imports: [FigurePipe, Legend, TranslocoPipe],
  template: `
    <svg
      viewBox="0 0 180 180"
      class="mx-auto block w-full max-w-44"
      role="img"
      [attr.aria-label]="label()"
    >
      @if (arcs().length === 0) {
        <text x="90" y="94" text-anchor="middle" class="donut-empty">
          {{ 'admin.chart.empty' | transloco }}
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
        <text x="90" y="88" text-anchor="middle" class="donut-total">{{ total() }}</text>
        <text x="90" y="106" text-anchor="middle" class="donut-caption">{{ caption() }}</text>
      }
    </svg>
    @if (legend().length > 0) {
      <lodb-legend [items]="legend()" />
    }
  `,
  styles: `
    .donut-track {
      stroke: color-mix(in srgb, var(--color-gold-deep) 30%, transparent);
    }
    .donut-total {
      font-family: var(--font-beaufort);
      font-size: 1.35rem;
      fill: var(--color-gold-bright);
    }
    .donut-caption,
    .donut-empty {
      font-size: 0.7rem;
      letter-spacing: 0.12em;
      text-transform: uppercase;
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

  protected readonly arcs = computed(() => donutArcs(this.slices()));
  protected readonly legend = computed(() =>
    this.arcs().map((arc) => ({
      label: arc.name,
      color: arc.color,
      value: figure(arc.pct, 'pct'),
    })),
  );
}
