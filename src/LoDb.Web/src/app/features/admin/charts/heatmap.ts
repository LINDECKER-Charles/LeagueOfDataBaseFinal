import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { heatColor } from './scale/heat-color';

/** A cell of the grid: its day (0 is Monday), its hour, its views and its fill. */
interface HeatCell {
  readonly day: number;
  readonly hour: number;
  readonly views: number;
  readonly color: string;
}

const DAYS = 7;
const HOURS = 24;
// A label every six hours keeps the header readable on a phone.
const HOUR_LABEL_EVERY = 6;

/**
 * The rhythm of the site: the views of each hour of each day of the week, the busiest cell
 * in full cyan. Each cell is titled with its day, its hour and its views, which a pointer
 * reads on hover; the grid as a whole is named for assistive technology.
 */
@Component({
  selector: 'lodb-heatmap',
  imports: [TranslocoPipe],
  template: `
    <div class="heat" role="img" [attr.aria-label]="'admin.heatmap.label' | transloco">
      <span></span>
      @for (hour of hours; track hour) {
        <span class="heat-hour">{{ hour % hourLabelEvery === 0 ? hour : '' }}</span>
      }
      @for (row of rows(); track $index; let day = $index) {
        <span class="heat-day">{{ 'admin.heatmap.days.' + day | transloco }}</span>
        @for (cell of row; track cell.hour) {
          <span
            class="heat-cell"
            [style.background]="cell.color"
            [title]="
              'admin.heatmap.cell'
                | transloco
                  : {
                      day: ('admin.heatmap.days.' + cell.day | transloco),
                      hour: cell.hour,
                      views: cell.views,
                    }
            "
          ></span>
        }
      }
    </div>
  `,
  styles: `
    .heat {
      display: grid;
      grid-template-columns: 2.5rem repeat(24, minmax(0, 1fr));
      gap: 2px;
      font-family: var(--font-mono);
      font-size: 0.65rem;
      color: var(--color-text-dim);
    }
    .heat-cell {
      aspect-ratio: 1;
      min-block-size: 0.6rem;
    }
    .heat-day {
      align-self: center;
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Heatmap {
  /** Seven rows, Monday first, of 24 hours of views. */
  readonly grid = input.required<readonly (readonly number[])[]>();

  protected readonly hours = Array.from({ length: HOURS }, (_, hour) => hour);
  protected readonly hourLabelEvery = HOUR_LABEL_EVERY;
  protected readonly rows = computed(() => this.cellsOf(this.grid()));

  private cellsOf(grid: readonly (readonly number[])[]): readonly (readonly HeatCell[])[] {
    const max = Math.max(0, ...grid.flat());
    return Array.from({ length: DAYS }, (_, day) =>
      this.hours.map((hour) => {
        const views = grid[day]?.[hour] ?? 0;
        return { day, hour, views, color: heatColor(views, max) };
      }),
    );
  }
}
