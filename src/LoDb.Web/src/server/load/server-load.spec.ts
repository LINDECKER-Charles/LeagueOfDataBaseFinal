import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { RenderCounts } from '../admission/render-counts';
import { RenderDurations } from './render-durations';
import { ServerLoad } from './server-load';

describe('ServerLoad', () => {
  let folder: string;
  let counts: RenderCounts;
  let durations: RenderDurations;
  let p99: number | null;
  const load = () =>
    new ServerLoad(counts, {
      durations,
      eventLoopP99Seconds: () => p99,
      cgroupFolder: folder,
    }).report();

  beforeEach(async () => {
    folder = await mkdtemp(join(tmpdir(), 'lodb-load-'));
    counts = new RenderCounts();
    durations = new RenderDurations();
    p99 = null;
  });

  afterEach(async () => {
    await rm(folder, { recursive: true, force: true });
  });

  it('reports every field, typed as the regulator reads them', async () => {
    await writeFile(join(folder, 'cpu.pressure'), 'some avg10=4.00 avg60=0 avg300=0 total=1');
    await writeFile(join(folder, 'cpu.stat'), 'usage_usec 10\nthrottled_usec 500000');
    durations.add('current', 0.2);
    counts.record('current', null);
    p99 = 0.012;

    const report = await load();

    expect(report.cpuSeconds).toBeGreaterThan(0);
    expect(report.eventLoopDelayP99Seconds).toBe(0.012);
    expect(report.cpuPressureSomeAvg10).toBeCloseTo(0.04);
    expect(report.cpuThrottledSeconds).toBe(0.5);
    expect(Object.keys(report.lanes).sort()).toEqual(['current', 'pinned', 'prerender']);
    expect(report.lanes.current).toEqual({ renders: 1, renderSeconds: 0.2, refused: 0 });
    expect(JSON.parse(JSON.stringify(report))).toEqual(report);
  });

  it("counts the server's and the lane's refusals, never a client's own share", async () => {
    counts.record('current', 'capacity');
    counts.record('current', 'lane_capacity');
    counts.record('current', 'lane_rate');
    counts.record('current', 'client_capacity');
    counts.record('current', 'client_rate');
    counts.record('pinned', 'lane_rate');
    counts.record('current', null);

    const { lanes } = await load();

    expect(lanes.current.refused).toBe(3);
    expect(lanes.pinned.refused).toBe(1);
    expect(lanes.prerender.refused).toBe(0);
  });

  it('gives null for the cgroup outside cgroup v2', async () => {
    const report = await load();

    expect(report.cpuPressureSomeAvg10).toBeNull();
    expect(report.cpuThrottledSeconds).toBeNull();
    expect(report.eventLoopDelayP99Seconds).toBeNull();
  });
});
