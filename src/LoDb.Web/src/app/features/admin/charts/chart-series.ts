import type { FigureFormat } from '../format/figure';

/** One line of a time-series chart: its values in the order of the dates. */
export interface ChartSeries {
  /** Its name, translated: the legend and the tooltip say it. */
  readonly label: string;
  /** A CSS colour, a token: `var(--color-gold)`. */
  readonly color: string;
  readonly values: readonly number[];
  /** How the tooltip writes a value; `int` by default. */
  readonly format?: FigureFormat;
}
