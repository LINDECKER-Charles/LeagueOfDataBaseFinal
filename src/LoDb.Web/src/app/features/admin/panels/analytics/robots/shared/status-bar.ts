import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { RobotFigures } from '../../../../../../core/api/generated/models/robot-figures';
import { figure } from '../../../../format/figure';
import { statusColor } from './robot-palette';

/** A part of the bar: its class, its count, its share of the whole. */
interface Part {
  readonly name: string;
  readonly count: number;
  readonly pct: number;
  readonly color: string;
}

const PERCENT = 100;

/**
 * How a robot was answered, in one thin bar: the 2xx in green, the 3xx in blue, the 4xx in
 * gold, the 5xx in red, each as wide as its share and titled with its count.
 */
@Component({
  selector: 'lodb-status-bar',
  template: `
    <span class="flex h-1.5 w-full min-w-[5rem] overflow-hidden bg-track" [attr.title]="title()">
      @for (part of parts(); track part.name) {
        <span
          class="block h-full"
          [style.inline-size.%]="part.pct"
          [style.background]="part.color"
        ></span>
      }
    </span>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusBar {
  readonly figures = input.required<RobotFigures>();

  protected readonly parts = computed<readonly Part[]>(() => {
    const figures = this.figures();
    const total = Math.max(figures.hits, 1);
    return [
      { name: '2xx', count: figures.success },
      { name: '3xx', count: figures.redirects },
      { name: '4xx', count: figures.clientErrors },
      { name: '5xx', count: figures.serverErrors },
    ]
      .filter((part) => part.count > 0)
      .map((part) => ({
        ...part,
        pct: (part.count / total) * PERCENT,
        color: statusColor(part.name),
      }));
  });
  protected readonly title = computed(() =>
    this.parts()
      .map((part) => `${part.name} ${figure(part.count)}`)
      .join(' · '),
  );
}
