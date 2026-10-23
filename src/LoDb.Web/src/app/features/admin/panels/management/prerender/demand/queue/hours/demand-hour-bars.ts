import type { PrerenderDemandHour } from '../../../../../../../../core/api/generated/models/prerender-demand-hour';

/** One hour of the chart: its UTC hour, its two counts, and their heights on the scale. */
export interface HourBar {
  readonly start: string;
  /** The hour in UTC, two digits: `07`. */
  readonly hour: string;
  readonly entered: number;
  readonly written: number;
  /** Heights out of 1, against the busiest count of the day. */
  readonly enteredShare: number;
  readonly writtenShare: number;
}

/** The hours, and the count the tallest bar stands for. */
export interface HourBars {
  readonly bars: readonly HourBar[];
  readonly peak: number;
}

const HOUR_DIGITS = 2;

/**
 * The bars of the last 24 hours of the queue, on one scale for both counts so that entered
 * and written compare at a glance; a day without any page keeps a scale of one.
 */
export function demandHourBars(hours: readonly PrerenderDemandHour[]): HourBars {
  const peak = Math.max(1, ...hours.flatMap((hour) => [hour.entered, hour.written]));
  return {
    peak,
    bars: hours.map((hour) => ({
      start: hour.start,
      hour: String(new Date(hour.start).getUTCHours()).padStart(HOUR_DIGITS, '0'),
      entered: hour.entered,
      written: hour.written,
      enteredShare: hour.entered / peak,
      writtenShare: hour.written / peak,
    })),
  };
}
