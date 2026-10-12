import { CHECK_INTERVAL_MS, CheckSchedule } from './check-schedule';

describe('CheckSchedule', () => {
  it('checks at once when nothing was checked yet', () => {
    expect(new CheckSchedule().isDue(0)).toBe(true);
  });

  it('checks again after the interval, never sooner', () => {
    const schedule = new CheckSchedule();
    schedule.record(1_000);

    expect(schedule.isDue(1_000 + CHECK_INTERVAL_MS - 1)).toBe(false);
    expect(schedule.isDue(1_000 + CHECK_INTERVAL_MS)).toBe(true);
  });
});
