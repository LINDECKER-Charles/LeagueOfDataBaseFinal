import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** An entry of a legend: a swatch, a label and, for a donut, the value of its slice. */
export interface LegendItem {
  readonly label: string;
  /** A CSS colour, a token. */
  readonly color: string;
  readonly value?: string;
}

/** The legend beside a chart: one swatch per series or slice. */
@Component({
  selector: 'lodb-legend',
  template: `
    <ul class="mt-3 flex flex-wrap gap-x-4 gap-y-1.5 text-xs text-text-muted">
      @for (item of items(); track item.label) {
        <li class="flex items-center gap-1.5">
          <span class="inline-block size-2.5" [style.background]="item.color"></span>
          {{ item.label }}
          @if (item.value) {
            <b class="font-mono font-medium text-text">{{ item.value }}</b>
          }
        </li>
      }
    </ul>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Legend {
  readonly items = input.required<readonly LegendItem[]>();
}
