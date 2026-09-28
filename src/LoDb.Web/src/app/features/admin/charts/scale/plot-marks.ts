import type { ChartSeries } from '../chart-series';
import { type FigureFormat, figure } from '../../format/figure';
import type { ChartScale } from './chart-scale';
import { type ChartPoint, polylinePoints } from './polyline-points';

/** A line of the grid and its value, written on the left margin. */
interface GridLine {
  readonly y: number;
  readonly labelX: number;
  readonly labelY: number;
  readonly label: string;
}

/**
 * The marks of one series: its line, its area when it has one, and a dot for a lone point,
 * which no line can draw (the legacy chart dotted a series of one day, its static fallback).
 */
interface PlotLine {
  readonly color: string;
  readonly line: string;
  readonly area: string | null;
  readonly dot: ChartPoint | null;
}

/** What a time-series chart draws for the current view. */
export interface PlotMarks {
  readonly max: number;
  readonly grid: readonly GridLine[];
  readonly lines: readonly PlotLine[];
}

const GRID_STEPS = [0, 0.5, 1] as const;
// Between a grid line and its label, and down to the optical centre of the line.
const LABEL_GAP = 6;
const LABEL_BASELINE = 3;
const CENTS = 100;
const BYTE_STEP = 1024;
const BYTE_LETTERS = ['B', 'K', 'M', 'G', 'T'] as const;
// Below ten units a size keeps one decimal: 1.5G, then 15G.
const DECIMAL_BELOW = 10;
const EMPTY_DECIMAL = /\.0$/;

function shortBytes(value: number): string {
  let unit = 0;
  let scaled = value;
  while (scaled >= BYTE_STEP && unit < BYTE_LETTERS.length - 1) {
    scaled /= BYTE_STEP;
    unit++;
  }
  const digits = scaled < DECIMAL_BELOW ? 1 : 0;
  return `${scaled.toFixed(digits).replace(EMPTY_DECIMAL, '')}${BYTE_LETTERS[unit] ?? ''}`;
}

// The labels of the Y axis fit its narrow margin: amounts in whole euros, sizes in short
// binary units, counts compact. The tooltip gives the exact value.
function axisLabel(value: number, format: FigureFormat | undefined): string {
  if (format === 'euros') {
    return `${figure(value / CENTS, 'compact')}€`;
  }
  return format === 'bytes' ? shortBytes(value) : figure(value, 'compact');
}

function gridOf(scale: ChartScale, max: number, format?: FigureFormat): readonly GridLine[] {
  return GRID_STEPS.map((step) => {
    const y = scale.y(max * step, max);
    return {
      y,
      labelX: scale.box.padX - LABEL_GAP,
      labelY: y + LABEL_BASELINE,
      label: axisLabel(max * step, format),
    };
  });
}

function lineOf(series: ChartSeries, scale: ChartScale, max: number): PlotLine {
  const points = series.values.map((value, index): ChartPoint => [
    scale.screenX(index),
    scale.y(value, max),
  ]);
  const baseline = scale.y(0, max);
  const first = points[0];
  const last = points[points.length - 1];
  const area =
    first && last ? polylinePoints([[first[0], baseline], ...points, [last[0], baseline]]) : null;
  return {
    color: series.color,
    line: polylinePoints(points),
    area,
    dot: points.length === 1 ? (first ?? null) : null,
  };
}

/**
 * The marks of a time-series chart in the current view: the grid scaled on the highest value
 * of every series, then each series placed on the screen, zoom and pan applied. The chart
 * clips them to the plot, so the points panned out of it fall outside.
 */
export function plotMarks(series: readonly ChartSeries[], scale: ChartScale): PlotMarks {
  const max = Math.max(0, ...series.flatMap((line) => line.values));
  return {
    max,
    // The first series sets the unit of the axis: overlaid series share one.
    grid: gridOf(scale, max, series[0]?.format),
    // Only the first series keeps its area: stacked fills would read as parts of a whole.
    lines: series.map((line, index) => {
      const marks = lineOf(line, scale, max);
      return index === 0 ? marks : { ...marks, area: null };
    }),
  };
}
