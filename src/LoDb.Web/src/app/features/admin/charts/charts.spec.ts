import type { Type } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { configureAdminTestBed } from '../testing/admin-test-bed';
import type { ChartSeries } from './chart-series';
import { DonutChart } from './donut-chart';
import { Heatmap } from './heatmap';
import { paletteSlices } from './palette-slices';
import { ChartScale } from './scale/chart-scale';
import { plotMarks } from './scale/plot-marks';
import { Sparkline } from './sparkline';

function render<T>(type: Type<T>, inputs: Readonly<Record<string, unknown>>): ComponentFixture<T> {
  const fixture = TestBed.createComponent(type);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  return fixture;
}

function all(fixture: ComponentFixture<unknown>, selector: string): Element[] {
  return [...(fixture.nativeElement as HTMLElement).querySelectorAll(selector)];
}

function words(element: Element | null | undefined): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

function series(values: readonly number[], format?: ChartSeries['format']): ChartSeries {
  return { label: 'Vues', color: 'var(--color-gold)', values, format };
}

describe('the SVG charts of the admin', () => {
  beforeEach(() => configureAdminTestBed());

  it('draws a sparkline as a line over its area, hidden from assistive technology', () => {
    const fixture = render(Sparkline, { values: [1, 4, 2], color: 'var(--color-gold)' });

    const svg = all(fixture, 'svg')[0];
    expect(svg?.getAttribute('aria-hidden')).toBe('true');
    expect(all(fixture, 'polyline')[0]?.getAttribute('stroke')).toBe('var(--color-gold)');
    expect(all(fixture, 'polygon')[0]?.getAttribute('fill')).toBe('var(--color-gold)');
  });

  it('draws no sparkline under two values', () => {
    expect(all(render(Sparkline, { values: [3] }), 'svg')).toHaveLength(0);
  });

  it('rings each slice of a donut, titled with its share, and repeats it in the legend', () => {
    const fixture = render(DonutChart, {
      slices: [
        { name: 'Mobile', value: 30, color: 'var(--color-hex)' },
        { name: 'Desktop', value: 10, color: 'var(--color-gold)' },
      ],
      label: 'Appareils',
      total: '40',
      caption: 'vues',
    });

    expect(all(fixture, 'svg')[0]?.getAttribute('aria-label')).toBe('Appareils');
    const arcs = all(fixture, 'circle[stroke-dasharray]');
    expect(arcs.map((arc) => arc.getAttribute('stroke'))).toEqual([
      'var(--color-hex)',
      'var(--color-gold)',
    ]);
    expect(words(arcs[0]?.querySelector('title'))).toBe('Mobile — 30 (75.0 %)');
    expect(all(fixture, 'text').map(words)).toEqual(['40', 'vues']);
    expect(all(fixture, 'lodb-legend li').map(words)).toEqual(['Mobile 75.0 %', 'Desktop 25.0 %']);
  });

  it('says a donut of nothing is empty instead of drawing a bare ring', () => {
    const fixture = render(DonutChart, {
      slices: [{ name: 'Mobile', value: 0, color: 'var(--color-hex)' }],
      label: 'Appareils',
    });

    expect(all(fixture, 'circle')).toHaveLength(0);
    expect(all(fixture, 'text').map(words)).toEqual(['admin.chart.empty']);
    expect(all(fixture, 'lodb-legend')).toHaveLength(0);
  });

  it('lays the week out as 7 rows of 24 hours, the busiest hour in full cyan', () => {
    const views: Readonly<Record<string, number>> = { '2:13': 40, '2:14': 10 };
    const grid = Array.from({ length: 7 }, (_, day) =>
      Array.from({ length: 24 }, (_, hour) => views[`${day}:${hour}`] ?? 0),
    );
    const fixture = render(Heatmap, { grid });

    const cells = all(fixture, '.heat-cell') as HTMLElement[];
    expect(cells).toHaveLength(7 * 24);
    expect(cells[2 * 24 + 13]?.style.background).toContain('var(--color-hex) 100%');
    expect(cells[2 * 24 + 14]?.style.background).toContain('var(--color-hex)');
    expect(cells[0]?.style.background).toContain('var(--color-gold-deep)');
    expect(all(fixture, '.heat-day').map(words)[0]).toBe('admin.heatmap.days.0');
    expect(all(fixture, '.heat-hour').map(words).filter(Boolean)).toEqual(['0', '6', '12', '18']);
  });

  it('reads a short grid as a week without views', () => {
    const fixture = render(Heatmap, { grid: [[5]] });

    expect(all(fixture, '.heat-cell')).toHaveLength(7 * 24);
  });
});

describe('plotMarks', () => {
  const scale = ChartScale.of(3);

  it('scales the grid on the highest value of every series', () => {
    const marks = plotMarks([series([1, 2, 3]), series([4, 8, 6])], scale);

    expect(marks.max).toBe(8);
    expect(marks.grid.map((line) => line.label)).toEqual(['0', '4', '8']);
    expect(marks.grid[0]?.y).toBe(scale.y(0, 8));
    expect(marks.grid[2]?.y).toBe(scale.y(8, 8));
  });

  it('fills only the first series, so that overlaid ones do not read as parts', () => {
    const marks = plotMarks([series([1, 2, 3]), series([3, 2, 1])], scale);

    expect(marks.lines[0]?.area).not.toBeNull();
    expect(marks.lines[1]?.area).toBeNull();
    expect(marks.lines[0]?.dots).toHaveLength(3);
  });

  it('drops the dots of a long series', () => {
    const long = Array.from({ length: 60 }, (_, index) => index);

    expect(plotMarks([series(long)], ChartScale.of(60)).lines[0]?.dots).toEqual([]);
  });

  it('writes the axis in the unit of the first series, short enough for the margin', () => {
    const labels = (values: number[], format: ChartSeries['format']) =>
      plotMarks([series(values, format)], scale).grid.map((line) => line.label);

    expect(labels([0, 250_000], 'euros')).toEqual(['0€', '1.3k€', '2.5k€']);
    expect(labels([0, 3 * 1024 ** 3], 'bytes')).toEqual(['0B', '1.5G', '3G']);
    expect(labels([0, 40 * 1024 ** 2], 'bytes')).toEqual(['0B', '20M', '40M']);
    expect(labels([0, 2_000_000], undefined)).toEqual(['0', '1M', '2M']);
  });

  it('draws an empty series without an area', () => {
    const marks = plotMarks([series([])], ChartScale.of(0));

    expect(marks.max).toBe(0);
    expect(marks.lines[0]?.area).toBeNull();
    expect(marks.lines[0]?.line).toBe('');
  });
});

describe('paletteSlices', () => {
  const rows = (count: number) =>
    Array.from({ length: count }, (_, index) => ({ name: `r${index}`, value: 10 - index }));

  it('colours the slices in turn with the tokens of the palette', () => {
    const slices = paletteSlices(rows(2), 'Autres');

    expect(slices).toEqual([
      { name: 'r0', value: 10, color: 'var(--color-gold)' },
      { name: 'r1', value: 9, color: 'var(--color-hex)' },
    ]);
  });

  it('keeps six rows whole, and folds the smallest past them into the rest', () => {
    expect(paletteSlices(rows(6), 'Autres').map((slice) => slice.name)).toEqual([
      'r0',
      'r1',
      'r2',
      'r3',
      'r4',
      'r5',
    ]);

    const folded = paletteSlices(rows(8), 'Autres');
    expect(folded.map((slice) => slice.name)).toEqual(['r0', 'r1', 'r2', 'r3', 'r4', 'Autres']);
    expect(folded[5]).toEqual({ name: 'Autres', value: 5 + 4 + 3, color: 'var(--color-text-dim)' });
  });
});
