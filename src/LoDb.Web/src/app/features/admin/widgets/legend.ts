import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** An entry of a legend: a swatch, a label and, for a donut, the value of its slice. */
export interface LegendItem {
  readonly label: string;
  /** A CSS colour, a token. */
  readonly color: string;
  readonly value?: string;
}

/** The legend under a chart: a diamond per series or slice, as the legacy one drew them. */
@Component({
  selector: 'lodb-legend',
  template: `
    <ul class="mt-[0.9rem] flex flex-wrap gap-x-[1.1rem] gap-y-2 text-[0.78rem] text-text-muted">
      @for (item of items(); track item.label) {
        <li class="inline-flex items-center gap-[0.45rem]">
          <span class="inline-block size-2.5 flex-none rotate-45" [style.background]="item.color">
          </span>
          {{ item.label }}
          @if (item.value) {
            <b class="font-mono font-normal text-text">{{ item.value }}</b>
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
