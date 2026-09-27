import type { AnalyticsDay } from '../../../../core/api/generated/models/analytics-day';
import type { ChartSeries } from '../../charts/chart-series';
import type { AdminText } from '../../shared/inject-admin-text';
import type { LegendItem } from '../../widgets/legend';

/** The daily traffic of a report, as its chart and its legend draw it. */
export interface TrafficSeries {
  readonly dates: readonly string[];
  readonly series: readonly ChartSeries[];
  readonly legend: readonly LegendItem[];
}

// The views lead in gold, the people behind them in cyan. The robots, left out of both, are
// not drawn: the legacy charts never did.
const LINES = [
  {
    key: 'traffic.series.views',
    color: 'var(--color-gold)',
    pick: (day: AnalyticsDay) => day.views,
  },
  {
    key: 'traffic.series.visitors',
    color: 'var(--color-hex)',
    pick: (day: AnalyticsDay) => day.visitors,
  },
] as const;

/** Views and visitors day by day, labelled in French. */
export function trafficSeries(days: readonly AnalyticsDay[], texts: AdminText): TrafficSeries {
  const series = LINES.map((line) => ({
    label: texts.text(line.key),
    color: line.color,
    values: days.map(line.pick),
  }));
  return {
    dates: days.map((day) => day.date),
    series,
    legend: series.map((line) => ({ label: line.label, color: line.color })),
  };
}
