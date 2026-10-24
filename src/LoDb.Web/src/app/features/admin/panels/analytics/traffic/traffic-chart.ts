import type { AnalyticsDay } from '../../../../../core/api/generated/models/analytics-day';
import type { RobotReport } from '../../../../../core/api/generated/models/robot-report';
import type { AdminText } from '../../../shared/inject-admin-text';
import { totalOf } from '../robots/shared/robot-chart';
import { type TrafficSeries, trafficSeries } from '../traffic-series';

const ROBOT_COLOR = 'var(--color-series-red)';

/**
 * The days of the traffic page: the readers' views and visitors, then the robots' hits on
 * the same dates, once the journal answered; a day it did not count reads 0.
 */
export function trafficChart(
  days: readonly AnalyticsDay[],
  robots: RobotReport | undefined,
  texts: AdminText,
): TrafficSeries {
  const readers = trafficSeries(days, texts);
  if (!robots) {
    return readers;
  }
  const hits = totalOf(robots.categorySeries);
  const byDate = new Map(robots.dates.map((date, index) => [date, hits[index] ?? 0]));
  const line = {
    label: texts.text('traffic.series.robots'),
    color: ROBOT_COLOR,
    values: readers.dates.map((date) => byDate.get(date) ?? 0),
  };
  return {
    dates: readers.dates,
    series: [...readers.series, line],
    legend: [...readers.legend, { label: line.label, color: line.color }],
  };
}
