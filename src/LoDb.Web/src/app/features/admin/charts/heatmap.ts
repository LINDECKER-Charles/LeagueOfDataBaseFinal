import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { heatColor } from './scale/heat-color';
import { AdminTextPipe } from '../shared/admin-text-pipe';

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
  imports: [AdminTextPipe],
  template: `
    <div class="heat" role="img" [attr.aria-label]="'admin.heatmap.label' | adminText">
      <span></span>
      @for (hour of hours; track hour) {
        <span class="heat-hour">{{ hour % hourLabelEvery === 0 ? hour : '' }}</span>
      }
      @for (row of rows(); track $index; let day = $index) {
        <span class="heat-day">{{ 'admin.heatmap.days.' + day | adminText }}</span>
        @for (cell of row; track cell.hour) {
          <span
            class="heat-cell"
            [style.background]="cell.color"
            [title]="
              'admin.heatmap.cell'
                | adminText
                  : {
                      day: ('admin.heatmap.days.' + cell.day | adminText),
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
      grid-template-columns: 2.2rem repeat(24, minmax(0, 1fr));
      align-items: center;
      gap: 2px;
    }
    /* The hours every six: two digits never break, even over a 10px column of a phone. */
    .heat-hour {
      font-family: var(--font-mono);
      font-size: 0.6rem;
      text-align: center;
      white-space: nowrap;
      color: var(--color-text-dim);
    }
    .heat-day {
      font-family: var(--font-beaufort);
      font-size: 0.62rem;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--color-text-muted);
    }
    .heat-cell {
      aspect-ratio: 1;
      min-block-size: 12px;
      border: 1px solid color-mix(in srgb, var(--color-text) 3%, transparent);
    }
    .heat-cell:hover {
      outline: 1px solid var(--color-gold);
    }
    @media (width <= 860px) {
      .heat {
        grid-template-columns: 2rem repeat(24, minmax(0, 1fr));
      }
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
