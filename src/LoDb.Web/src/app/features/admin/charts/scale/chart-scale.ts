import { PLOT_BOX, type PlotBox } from './plot-box';

const MIN_SCALE = 1;
const MAX_SCALE = 24;
const DEFAULT_TICKS = 4;

// Where the viewport stands: how far it zooms, and how far the zoomed plot is shifted.
interface Viewport {
  readonly scale: number;
  readonly offset: number;
}

/**
 * The X axis of a time-series chart, zoomed and panned: pure viewport math, no DOM. Point
 * `i` of `count` sits at `padX + i * plotW / (count - 1)` before any zoom, a lone point in
 * the middle; the zoom multiplies that position by `scale` around the left edge of the plot,
 * then shifts it by `offset`. Only X zooms, so the Y grid stays valid as drawn. Immutable:
 * every gesture answers a new scale, which a signal holds.
 */
export class ChartScale {
  /** The factor of one step of zoom: a wheel notch, a button, a key. */
  static readonly zoomStep = 1.25;

  readonly scale: number;
  readonly offset: number;

  private constructor(
    readonly count: number,
    readonly box: PlotBox,
    viewport: Viewport,
  ) {
    this.scale = viewport.scale;
    this.offset = viewport.offset;
  }

  /** The whole series of `count` points, not zoomed. */
  static of(count: number, box: PlotBox = PLOT_BOX): ChartScale {
    return new ChartScale(Math.max(0, count), box, { scale: MIN_SCALE, offset: 0 });
  }

  get zoomed(): boolean {
    return this.scale > MIN_SCALE;
  }

  /** X of point `index` before the zoom. */
  modelX(index: number): number {
    const { padX, plotW } = this.box;
    return padX + (this.count <= 1 ? plotW / 2 : (index * plotW) / (this.count - 1));
  }

  /** X of point `index` on the screen, zoom and pan applied. */
  screenX(index: number): number {
    return this.toScreen(this.modelX(index));
  }

  toScreen(modelX: number): number {
    return this.scale * modelX + this.box.padX * (1 - this.scale) + this.offset;
  }

  toModel(screenX: number): number {
    return (screenX - this.box.padX * (1 - this.scale) - this.offset) / this.scale;
  }

  /** Whether a screen X falls inside the plot, margins excluded. */
  contains(screenX: number): boolean {
    return screenX >= this.box.padX && screenX <= this.box.padX + this.box.plotW;
  }

  /** The point nearest to a screen X, clamped to the series. */
  indexAt(screenX: number): number {
    if (this.count < 2) {
      return 0;
    }
    const { padX, plotW } = this.box;
    const raw = Math.round(((this.toModel(screenX) - padX) * (this.count - 1)) / plotW);
    return Math.min(this.count - 1, Math.max(0, raw));
  }

  /** Zooms by `factor`, the point under `screenX` staying where it is. */
  zoomAt(screenX: number, factor: number): ChartScale {
    const scale = Math.min(MAX_SCALE, Math.max(MIN_SCALE, this.scale * factor));
    const anchor = this.toModel(screenX);
    const offset = screenX - this.box.padX * (1 - scale) - scale * anchor;
    return this.with(scale, offset);
  }

  /** Pans by a screen distance; the plot never uncovers a blank margin. */
  panBy(deltaX: number): ChartScale {
    return this.with(this.scale, this.offset + deltaX);
  }

  reset(): ChartScale {
    return ChartScale.of(this.count, this.box);
  }

  /** The first and the last point inside the plot window, both included. */
  visibleRange(): readonly [number, number] {
    const from = this.indexAt(this.box.padX);
    const to = this.indexAt(this.box.padX + this.box.plotW);
    return [from, Math.max(from, to)];
  }

  /** The same view, panned just enough to show point `index`. */
  reveal(index: number): ChartScale {
    const x = this.screenX(index);
    if (this.contains(x)) {
      return this;
    }
    const target = x < this.box.padX ? this.box.padX : this.box.padX + this.box.plotW;
    return this.panBy(target - x);
  }

  /** Evenly spread labelled points of the visible range, both ends included. */
  ticks(wanted = DEFAULT_TICKS): readonly number[] {
    const [from, to] = this.visibleRange();
    const span = to - from;
    if (span <= 0) {
      return [from];
    }
    const steps = Math.min(wanted - 1, span);
    const ticks = Array.from({ length: steps + 1 }, (_, step) =>
      Math.round(from + (step * span) / steps),
    );
    return [...new Set(ticks)];
  }

  /** Y of a value on a plot whose top is `max`; zero sits on the baseline. */
  y(value: number, max: number): number {
    const ratio = max > 0 ? value / max : 0;
    return this.box.padTop + this.box.plotH - ratio * this.box.plotH;
  }

  // Both plot edges stay outside the window: a pan only ever reveals data.
  private with(scale: number, offset: number): ChartScale {
    const clamped = Math.min(0, Math.max(this.box.plotW * (1 - scale), offset));
    return new ChartScale(this.count, this.box, { scale, offset: clamped });
  }
}
