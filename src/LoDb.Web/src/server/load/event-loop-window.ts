import { monitorEventLoopDelay, type ELDHistogram } from 'node:perf_hooks';

/** The window the regulator reads the event loop's delay over. */
export const EVENT_LOOP_WINDOW_MS = 30_000;
// prom-client's own sampling precision: the two series stay comparable.
const RESOLUTION_MS = 10;
const P99 = 99;
const NANOSECONDS_PER_SECOND = 1e9;

/**
 * The p99 of the event loop's delay over the last closed window, from a monitor of its own.
 * prom-client resets its monitor at each scrape of `/metrics`: a second reader of that one
 * would cut the series the host's collector reads short, so the regulator never shares it.
 */
export class EventLoopWindow {
  private readonly histogram: ELDHistogram = monitorEventLoopDelay({
    resolution: RESOLUTION_MS,
  });
  private closedP99Seconds: number | null = null;
  private rotation: NodeJS.Timeout | null = null;

  /** Starts measuring; the timer never keeps the process alive. */
  start(): void {
    this.histogram.enable();
    this.rotation = setInterval(() => this.rotate(), EVENT_LOOP_WINDOW_MS);
    this.rotation.unref();
  }

  stop(): void {
    if (this.rotation !== null) {
      clearInterval(this.rotation);
      this.rotation = null;
    }
    this.histogram.disable();
  }

  /** The p99 of the last closed window; null until one closed. */
  p99Seconds(): number | null {
    return this.closedP99Seconds;
  }

  private rotate(): void {
    this.closedP99Seconds = this.histogram.percentile(P99) / NANOSECONDS_PER_SECOND;
    this.histogram.reset();
  }
}
