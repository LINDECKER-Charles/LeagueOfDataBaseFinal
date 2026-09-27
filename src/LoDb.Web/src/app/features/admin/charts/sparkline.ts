import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { sparklineShape } from './scale/sparkline-shape';

// Light enough to leave the line in front.
const AREA_OPACITY = 0.14;

/**
 * The trend of a tile, without axes: a line and a faint area, stretched to the width of the
 * tile. Decorative: the figure beside it says the value, so it is hidden from assistive
 * technology. Draws nothing under two values.
 */
@Component({
  selector: 'lodb-sparkline',
  template: `
    @if (shape(); as shape) {
      <svg
        class="block size-full"
        [attr.viewBox]="shape.viewBox"
        preserveAspectRatio="none"
        aria-hidden="true"
      >
        <polygon
          [attr.points]="shape.area"
          [attr.fill]="color()"
          [attr.fill-opacity]="areaOpacity"
        />
        <polyline
          [attr.points]="shape.line"
          fill="none"
          [attr.stroke]="color()"
          stroke-width="1.5"
          stroke-linejoin="round"
          vector-effect="non-scaling-stroke"
        />
      </svg>
    }
  `,
  host: { class: 'block size-full' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Sparkline {
  readonly values = input.required<readonly number[]>();
  /** A CSS colour, a token. */
  readonly color = input('var(--color-hex)');

  protected readonly shape = computed(() => sparklineShape(this.values()));
  protected readonly areaOpacity = AREA_OPACITY;
}
