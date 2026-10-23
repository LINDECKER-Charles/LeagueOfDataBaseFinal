import { demandHours } from '../../../../../../testing/fixtures/prerender-demand';
import { demandHourBars } from './demand-hour-bars';

describe('demandHourBars', () => {
  it('scales both counts against the busiest of the day, the hours in UTC', () => {
    const { bars, peak } = demandHourBars(demandHours());

    expect(peak).toBe(64);
    expect(bars).toHaveLength(24);
    expect(bars[0]).toMatchObject({ hour: '21', entered: 60, enteredShare: 60 / 64 });
    expect(bars[23]).toMatchObject({ hour: '20', written: 64, writtenShare: 1 });
  });

  it('keeps a scale of one over a day without any page', () => {
    const quiet = demandHours().map((hour) => ({ ...hour, entered: 0, written: 0 }));

    const { bars, peak } = demandHourBars(quiet);

    expect(peak).toBe(1);
    expect(bars.every((bar) => bar.enteredShare === 0 && bar.writtenShare === 0)).toBe(true);
  });
});
