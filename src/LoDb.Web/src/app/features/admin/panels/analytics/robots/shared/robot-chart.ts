import type { RobotSeries } from '../../../../../../core/api/generated/models/robot-series';
import type { ChartSeries } from '../../../../charts/chart-series';
import type { AdminText } from '../../../../shared/inject-admin-text';
import type { LegendItem } from '../../../../widgets/legend';
import { categoryColor, statusColor } from './robot-palette';

/** The lines of a chart of the robots and their legend. */
export interface RobotChart {
  readonly series: readonly ChartSeries[];
  readonly legend: readonly LegendItem[];
}

function chartOf(
  lines: readonly RobotSeries[],
  label: (name: string) => string,
  color: (name: string) => string,
): RobotChart {
  const series = lines.map((line) => ({
    label: label(line.name),
    color: color(line.name),
    values: line.values,
  }));
  return {
    series,
    legend: series.map((line) => ({ label: line.label, color: line.color })),
  };
}

/** A line per category of robots, each in its colour. */
export function categoryChart(lines: readonly RobotSeries[], texts: AdminText): RobotChart {
  return chartOf(lines, (name) => texts.term('robots.categories', name), categoryColor);
}

/** A line per class of statuses: 2xx in green to 5xx in red. */
export function statusChart(lines: readonly RobotSeries[]): RobotChart {
  return chartOf(lines, (name) => name, statusColor);
}

/** Every line summed, day by day: the robots' hits of each date. */
export function totalOf(lines: readonly RobotSeries[]): number[] {
  const days = lines[0]?.values.length ?? 0;
  return Array.from({ length: days }, (_, day) =>
    lines.reduce((sum, line) => sum + (line.values[day] ?? 0), 0),
  );
}
