import type { RenderLane } from '../admission/render-lane';

interface Totals {
  renders: number;
  renderSeconds: number;
}

/**
 * Renders that ended per lane and their cumulative duration, fed by the same timer as
 * `lodb_ssr_render_duration_seconds`: a reader takes the difference of two reads for a mean,
 * without touching the histogram the scrape exports.
 */
export class RenderDurations {
  private readonly totals: Record<RenderLane, Totals> = {
    pinned: { renders: 0, renderSeconds: 0 },
    current: { renders: 0, renderSeconds: 0 },
    prerender: { renders: 0, renderSeconds: 0 },
  };

  add(lane: RenderLane, seconds: number): void {
    const totals = this.totals[lane];
    totals.renders += 1;
    totals.renderSeconds += seconds;
  }

  /** The lane's totals so far, as a copy. */
  of(lane: RenderLane): Readonly<Totals> {
    return { ...this.totals[lane] };
  }
}
