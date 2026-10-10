import { createLogger } from '../create-logger';
import { AdmissionSummary } from './admission-summary';
import { RenderCounts } from './render-counts';

describe('AdmissionSummary', () => {
  let written: string;
  let counts: RenderCounts;
  let summary: AdmissionSummary;

  const lines = () =>
    written
      .split('\n')
      .filter((line) => line.length > 0)
      .map((line) => JSON.parse(line) as Record<string, unknown>);
  const repeat = (times: number, action: () => void) =>
    Array.from({ length: times }).forEach(action);

  beforeEach(() => {
    written = '';
    counts = new RenderCounts();
    const logger = createLogger({ write: (chunk: string) => (written += chunk) });
    summary = new AdmissionSummary(counts, logger);
  });

  it('logs one warning with the counts of the minute, zeros left out', () => {
    repeat(5, () => counts.record('current', null));
    repeat(3, () => counts.record('pinned', 'lane_rate'));
    counts.record('pinned', 'client_rate');

    summary.flush();

    expect(lines()).toEqual([
      expect.objectContaining({
        level: 'warn',
        msg: 'ssr.admission.summary',
        interval_seconds: 60,
        admitted: { current: 5 },
        refused: { pinned: { lane_rate: 3, client_rate: 1 } },
      }),
    ]);
  });

  it("tells a client over its share of the current pages from the lane's own budget", () => {
    repeat(4, () => counts.record('current', 'client_rate'));
    counts.record('current', 'lane_rate');

    summary.flush();

    expect(lines()[0]).toMatchObject({ refused: { current: { client_rate: 4, lane_rate: 1 } } });
  });

  it('counts each minute from the end of the previous one', () => {
    counts.record('current', 'capacity');
    summary.flush();
    counts.record('current', 'capacity');
    counts.record('pinned', null);

    summary.flush();

    expect(lines()[1]).toMatchObject({
      admitted: { pinned: 1 },
      refused: { current: { capacity: 1 } },
    });
  });

  it('logs nothing for a minute without refusal', () => {
    counts.record('pinned', 'lane_capacity');
    summary.flush();
    repeat(4, () => counts.record('current', null));

    summary.flush();

    expect(lines()).toHaveLength(1);
  });
});
