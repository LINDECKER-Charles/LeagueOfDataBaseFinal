import type { RenderLane } from '../admission/render-lane';

/** One lane's renders since the server started: cumulative, as a counter's. */
export interface LaneLoad {
  /** Renders that ended, whatever their status. */
  readonly renders: number;
  /** Their total duration, from admission to the end of the response. */
  readonly renderSeconds: number;
  /** Renders refused for the server's or the lane's sake, never a client's own share. */
  readonly refused: number;
}

/**
 * What `GET /load` answers on the metrics port: the renderer's load, read by the API's
 * pre-render regulator (docs/produit/pre-rendu-catalogue-cdn.md, lot 2). Counts and CPU
 * figures only: no path, no address, no personal data.
 */
export interface LoadReport {
  /** CPU the process used since it started, user and system. */
  readonly cpuSeconds: number;
  /** p99 of the event loop's delay over the last closed window; null before the first. */
  readonly eventLoopDelayP99Seconds: number | null;
  /** The container's `cpu.pressure` some avg10, as a 0..1 share; null outside cgroup v2. */
  readonly cpuPressureSomeAvg10: number | null;
  /** The container's throttled CPU time; null outside cgroup v2. */
  readonly cpuThrottledSeconds: number | null;
  readonly lanes: Readonly<Record<RenderLane, LaneLoad>>;
}
