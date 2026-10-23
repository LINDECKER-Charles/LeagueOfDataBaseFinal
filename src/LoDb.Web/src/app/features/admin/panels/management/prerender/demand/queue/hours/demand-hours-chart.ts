import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { PrerenderDemandHour } from '../../../../../../../../core/api/generated/models/prerender-demand-hour';
import { figure } from '../../../../../../format/figure';
import { AdminTextPipe } from '../../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../../shared/inject-admin-text';
import { Legend, type LegendItem } from '../../../../../../widgets/legend';
import { type HourBar, demandHourBars } from './demand-hour-bars';

const CHART = 'prerender.demand.queue.chart';
const ENTERED = 'var(--color-gold)';
const WRITTEN = 'var(--color-series-blue)';
const PERCENT = 100;
// An hour named under the bars every six, the current one too.
const TICK_EVERY = 6;

/**
 * The last 24 hours of the queue, an hour per pair of bars on one scale: the pages that
 * entered it (a visitor refused them) beside those it wrote. Each pair says its figures on
 * hover; assistive technology reads the same figures from a table.
 */
@Component({
  selector: 'lodb-demand-hours-chart',
  imports: [AdminTextPipe, Legend],
  template: `
    @let chart = chartOf();
    <div class="hours" role="img" [attr.aria-label]="label()">
      @for (bar of chart.bars; track bar.start; let index = $index) {
        <div class="hours__slot" [attr.title]="tooltip(bar)" data-hour>
          <span
            class="hours__bar"
            data-series="entered"
            [style.block-size.%]="bar.enteredShare * percent"
            [style.background]="entered"
          ></span>
          <span
            class="hours__bar"
            data-series="written"
            [style.block-size.%]="bar.writtenShare * percent"
            [style.background]="written"
          ></span>
          <span class="hours__tick" aria-hidden="true">{{ isTick(index) ? bar.hour : '' }}</span>
        </div>
      }
    </div>
    <lodb-legend [items]="legend()" />
    <table class="sr-only" data-hours-table>
      <caption>
        {{
          'admin.prerender.demand.queue.chart.caption' | adminText
        }}
      </caption>
      <thead>
        <tr>
          <th scope="col">{{ 'admin.prerender.demand.queue.chart.hour' | adminText }}</th>
          <th scope="col">{{ 'admin.prerender.demand.queue.chart.entered' | adminText }}</th>
          <th scope="col">{{ 'admin.prerender.demand.queue.chart.written' | adminText }}</th>
        </tr>
      </thead>
      <tbody>
        @for (bar of chart.bars; track bar.start) {
          <tr>
            <th scope="row">{{ bar.hour }}:00</th>
            <td>{{ bar.entered }}</td>
            <td>{{ bar.written }}</td>
          </tr>
        }
      </tbody>
    </table>
  `,
  styles: `
    .hours {
      display: grid;
      grid-template-columns: repeat(24, minmax(0, 1fr));
      gap: 2px;
      align-items: end;
      block-size: 7rem;
      padding-block-end: 1.1rem;
      border-block-end: 1px solid var(--color-track);
    }
    .hours__slot {
      position: relative;
      display: flex;
      gap: 1px;
      align-items: flex-end;
      block-size: 100%;
    }
    .hours__bar {
      flex: 1;
      min-block-size: 1px;
      border-start-start-radius: 2px;
      border-start-end-radius: 2px;
    }
    .hours__tick {
      position: absolute;
      inset-block-start: calc(100% + 0.2rem);
      inset-inline-start: 0;
      color: var(--color-text-dim);
      font-family: var(--font-mono);
      font-size: 0.7rem;
    }
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DemandHoursChart {
  /** The last 24 hours, the oldest first. */
  readonly hours = input.required<readonly PrerenderDemandHour[]>();

  private readonly texts = injectAdminText();

  protected readonly percent = PERCENT;
  protected readonly entered = ENTERED;
  protected readonly written = WRITTEN;
  protected readonly chartOf = computed(() => demandHourBars(this.hours()));
  protected readonly label = computed(() =>
    this.texts.text(`${CHART}.label`, { peak: figure(this.chartOf().peak) }),
  );
  protected readonly legend = computed<LegendItem[]>(() => [
    { label: this.texts.text(`${CHART}.entered`), color: ENTERED },
    { label: this.texts.text(`${CHART}.written`), color: WRITTEN },
  ]);

  protected tooltip(bar: HourBar): string {
    return this.texts.text(`${CHART}.tooltip`, {
      hour: bar.hour,
      entered: bar.entered,
      written: bar.written,
    });
  }

  protected isTick(index: number): boolean {
    return index % TICK_EVERY === 0 || index === this.chartOf().bars.length - 1;
  }
}
