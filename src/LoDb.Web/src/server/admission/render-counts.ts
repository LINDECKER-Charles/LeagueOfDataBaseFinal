import type { RenderLane } from './render-lane';
import type { RenderRefusal } from './render-refusal';

interface Series {
  readonly lane: RenderLane;
  /** Null for the renders admitted. */
  readonly refusal: RenderRefusal | null;
  count: number;
}

/**
 * Renders the admission decided on since the server started, per lane and per refusal. The
 * counts only grow, as a Prometheus counter's: `lodb_ssr_renders_total{lane,outcome,reason}`
 * can export them as they are, and the minute summary logs their increase.
 */
export class RenderCounts {
  private readonly series = new Map<string, Series>();

  record(lane: RenderLane, refusal: RenderRefusal | null): void {
    const key = `${lane}|${refusal ?? ''}`;
    const known = this.series.get(key);
    if (known === undefined) {
      this.series.set(key, { lane, refusal, count: 1 });
    } else {
      known.count += 1;
    }
  }

  /** Each series counted so far, as a copy. */
  entries(): readonly Readonly<Series>[] {
    return [...this.series.values()].map((series) => ({ ...series }));
  }
}
