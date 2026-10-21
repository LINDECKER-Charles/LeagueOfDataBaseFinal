import { pollDelay } from './poll-delay';

describe('pollDelay', () => {
  it('waits the cadence itself while the reads succeed', () => {
    expect(pollDelay(0, 5_000)).toBe(5_000);
  });

  it('doubles the wait at each failure in a row', () => {
    expect([1, 2, 3].map((failures) => pollDelay(failures, 5_000))).toEqual([
      10_000, 20_000, 40_000,
    ]);
  });

  it('backs off to a minute at most for a fast cadence', () => {
    expect(pollDelay(4, 5_000)).toBe(60_000);
    expect(pollDelay(12, 5_000)).toBe(60_000);
  });

  it('backs off to five cadences for a slow one', () => {
    expect([1, 2, 3, 4].map((failures) => pollDelay(failures, 60_000))).toEqual([
      120_000, 240_000, 300_000, 300_000,
    ]);
  });
});
