import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  computed,
  input,
  linkedSignal,
  viewChild,
} from '@angular/core';
import { figure } from '../format/figure';
import type { ChartSeries } from './chart-series';
import { ChartScale } from './scale/chart-scale';
import { PLOT_BOX } from './scale/plot-box';
import { plotMarks } from './scale/plot-marks';
import { AdminTextPipe } from '../shared/admin-text-pipe';

/** A labelled date under the plot. */
interface AxisTick {
  readonly index: number;
  readonly x: number;
  readonly text: string;
  readonly anchor: 'start' | 'middle' | 'end';
}

/** The point under the crosshair: its dots and the values the tooltip says. */
interface Cursor {
  readonly x: number;
  readonly date: string;
  readonly dots: readonly { readonly y: number; readonly color: string }[];
  readonly rows: readonly {
    readonly label: string;
    readonly color: string;
    readonly value: string;
  }[];
  /** Where the tooltip stands, in percent of the width; flipped on the right half. */
  readonly left: number;
  readonly flipped: boolean;
}

// A press that moves less than this is a click, not a pan.
const DRAG_THRESHOLD = 3;
// Under the baseline, the dates clear the axis.
const AXIS_DROP = 16;
// An ISO date without its year: the tick says MM-DD.
const MONTH_DAY = 5;
const PERCENT = 100;
let lastChart = 0;

/**
 * Overlaid series on one date axis, explorable: the wheel, the buttons or `+` and `-` zoom
 * the X axis around the pointer, a drag pans a zoomed chart, a double click or `Home`
 * resets it; the crosshair follows the pointer or the arrows and a tooltip says the exact
 * value of every series at that date. As in the legacy admin, the lines carry no marker:
 * only the crosshair dots the point it reads. The legacy enhanced a server SVG; here the
 * marks are drawn from the view.
 */
@Component({
  selector: 'lodb-time-series-chart',
  imports: [AdminTextPipe],
  templateUrl: './time-series-chart.html',
  styleUrl: './time-series-chart.css',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TimeSeriesChart {
  /** The ISO dates of the points, oldest first. */
  readonly dates = input.required<readonly string[]>();
  readonly series = input.required<readonly ChartSeries[]>();
  /** The accessible name of the chart, translated. */
  readonly label = input.required<string>();

  private readonly plot = viewChild<ElementRef<SVGSVGElement>>('plot');
  private drag: { readonly originX: number; readonly view: ChartScale } | null = null;
  private readonly keys: Readonly<Record<string, () => void>> = {
    ArrowLeft: () => this.moveHover(-1),
    ArrowRight: () => this.moveHover(1),
    '+': () => this.zoomIn(),
    '=': () => this.zoomIn(),
    '-': () => this.zoomOut(),
    Home: () => this.reset(),
    Escape: () => this.hover.set(null),
  };

  protected readonly box = PLOT_BOX;
  protected readonly viewBox = `0 0 ${PLOT_BOX.w} ${PLOT_BOX.h}`;
  protected readonly axisY = PLOT_BOX.padTop + PLOT_BOX.plotH + AXIS_DROP;
  protected readonly tipTop = (PLOT_BOX.padTop / PLOT_BOX.h) * PERCENT;
  protected readonly clipId = `lodb-plot-${++lastChart}`;
  protected readonly hintId = `${this.clipId}-hint`;
  protected readonly view = linkedSignal(() => ChartScale.of(this.dates().length));
  protected readonly hover = linkedSignal<number, number | null>({
    source: () => this.dates().length,
    computation: () => null,
  });
  protected readonly marks = computed(() => plotMarks(this.series(), this.view()));
  protected readonly axis = computed(() => this.ticksOf(this.view()));
  protected readonly cursor = computed(() => this.cursorAt(this.hover()));

  protected zoomIn(): void {
    this.zoom(ChartScale.zoomStep);
  }

  // Out of the whole view only: at it, the tool and its key do nothing.
  protected zoomOut(): void {
    if (this.view().zoomed) {
      this.zoom(1 / ChartScale.zoomStep);
    }
  }

  protected reset(): void {
    if (this.view().zoomed) {
      this.view.update((view) => view.reset());
    }
  }

  protected onKeydown(event: KeyboardEvent): void {
    const action = this.keys[event.key];
    if (action) {
      event.preventDefault();
      action();
    }
  }

  protected onWheel(event: WheelEvent): void {
    event.preventDefault();
    const factor = event.deltaY < 0 ? ChartScale.zoomStep : 1 / ChartScale.zoomStep;
    const x = this.pointerX(event);
    this.view.update((view) => view.zoomAt(x, factor));
  }

  protected onPointerDown(event: PointerEvent): void {
    if (!this.view().zoomed) {
      return;
    }
    this.drag = { originX: this.pointerX(event), view: this.view() };
    const target = event.target as Element;
    target.setPointerCapture?.(event.pointerId);
  }

  protected onPointerMove(event: PointerEvent): void {
    const x = this.pointerX(event);
    if (this.drag && Math.abs(x - this.drag.originX) > DRAG_THRESHOLD) {
      this.view.set(this.drag.view.panBy(x - this.drag.originX));
    }
    const view = this.view();
    this.hover.set(view.contains(x) ? view.indexAt(x) : null);
  }

  protected endDrag(): void {
    this.drag = null;
  }

  protected onLeave(): void {
    this.drag = null;
    this.hover.set(null);
  }

  // Around the pointed point when there is one, the middle of the plot otherwise.
  private zoom(factor: number): void {
    const hover = this.hover();
    const view = this.view();
    const x = hover === null ? view.box.padX + view.box.plotW / 2 : view.screenX(hover);
    this.view.set(view.zoomAt(x, factor));
  }

  // Moves the crosshair, and pans just enough to keep it in the window.
  private moveHover(step: number): void {
    const last = this.dates().length - 1;
    const next = Math.min(last, Math.max(0, (this.hover() ?? 0) + step));
    this.hover.set(next);
    this.view.update((view) => view.reveal(next));
  }

  // The pointer in SVG units: the viewBox maps the box onto the drawn width.
  private pointerX(event: MouseEvent): number {
    const rect = this.plot()?.nativeElement.getBoundingClientRect();
    const width = rect?.width || this.box.w;
    return ((event.clientX - (rect?.left ?? 0)) * this.box.w) / width;
  }

  private ticksOf(view: ChartScale): readonly AxisTick[] {
    const ticks = view.ticks();
    return ticks.map((index, position) => ({
      index,
      x: view.screenX(index),
      text: (this.dates()[index] ?? '').slice(MONTH_DAY),
      anchor: position === 0 ? 'start' : position === ticks.length - 1 ? 'end' : 'middle',
    }));
  }

  private cursorAt(index: number | null): Cursor | null {
    if (index === null) {
      return null;
    }
    const view = this.view();
    const { max } = this.marks();
    const x = view.screenX(index);
    const series = this.series();
    return {
      x,
      date: this.dates()[index] ?? '',
      dots: series.map((line) => ({ y: view.y(line.values[index] ?? 0, max), color: line.color })),
      rows: series.map((line) => ({
        label: line.label,
        color: line.color,
        value: figure(line.values[index] ?? 0, line.format),
      })),
      left: (x / this.box.w) * PERCENT,
      flipped: x > this.box.w / 2,
    };
  }
}
