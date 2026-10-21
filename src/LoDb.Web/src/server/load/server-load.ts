import type { RenderCounts } from '../admission/render-counts';
import type { RenderLane } from '../admission/render-lane';
import type { RenderRefusal } from '../admission/render-refusal';
import { readCgroupCpu } from './cgroup-cpu';
import type { LaneLoad, LoadReport } from './load-report';
import type { RenderDurations } from './render-durations';

const LANES: readonly RenderLane[] = ['current', 'pinned', 'prerender'];
// The server or the lane had no room: a client that spends its own share says nothing of
// the load.
const SHARED_REFUSALS: ReadonlySet<RenderRefusal> = new Set([
  'capacity',
  'lane_capacity',
  'lane_rate',
]);
const MICROSECONDS_PER_SECOND = 1e6;

/** Where the load is read from, besides the admission's counts. */
export interface LoadSources {
  readonly durations: RenderDurations;
  readonly eventLoopP99Seconds: () => number | null;
  readonly cgroupFolder: string;
}

function refusedOf(counts: RenderCounts, lane: RenderLane): number {
  return counts
    .entries()
    .filter((series) => series.lane === lane && series.refusal !== null)
    .filter((series) => SHARED_REFUSALS.has(series.refusal as RenderRefusal))
    .reduce((total, series) => total + series.count, 0);
}

/**
 * The renderer's load as `GET /load` answers it. Reading it has no side effect: no metric is
 * collected, so the scrape of `/metrics` stays the only reader of prom-client's monitors.
 */
export class ServerLoad {
  constructor(
    private readonly counts: RenderCounts,
    private readonly sources: LoadSources,
  ) {}

  async report(): Promise<LoadReport> {
    const cgroup = await readCgroupCpu(this.sources.cgroupFolder);
    const usage = process.cpuUsage();
    return {
      cpuSeconds: (usage.user + usage.system) / MICROSECONDS_PER_SECOND,
      eventLoopDelayP99Seconds: this.sources.eventLoopP99Seconds(),
      cpuPressureSomeAvg10: cgroup.pressure,
      cpuThrottledSeconds: cgroup.throttledSeconds,
      lanes: Object.fromEntries(LANES.map((lane) => [lane, this.laneOf(lane)])) as Record<
        RenderLane,
        LaneLoad
      >,
    };
  }

  private laneOf(lane: RenderLane): LaneLoad {
    return { ...this.sources.durations.of(lane), refused: refusedOf(this.counts, lane) };
  }
}
