import type { Type } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { configureAdminTestBed } from '../testing/admin-test-bed';
import { categorySlices } from './category-slices';
import type { ChartSeries } from './chart-series';
import { DonutChart } from './donut-chart';
import { Heatmap } from './heatmap';
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

  it('rings each slice of a donut, titled with its share, its count in the legend', () => {
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
    expect(all(fixture, 'lodb-legend li').map(words)).toEqual(['Mobile 30', 'Desktop 10']);
  });

  it('draws the ring at the start of the canvas every legacy chart shares', () => {
    const fixture = render(DonutChart, {
      slices: [{ name: 'Mobile', value: 3, color: 'var(--color-hex)' }],
      label: 'Appareils',
      total: '3',
    });

    expect(all(fixture, 'svg')[0]?.getAttribute('viewBox')).toBe('0 0 760 240');
    const rings = all(fixture, 'circle').map((ring) => [
      ring.getAttribute('cx'),
      ring.getAttribute('cy'),
    ]);
    expect(rings).toEqual([
      ['90', '120'],
      ['90', '120'],
    ]);
    expect(all(fixture, 'text.donut-total')[0]?.getAttribute('x')).toBe('90');
  });

  it('squeezes a total too long for the hole of the ring into it', () => {
    const fixture = render(DonutChart, {
      slices: [{ name: 'data', value: 1, color: 'var(--color-gold)' }],
      label: 'Familles',
      total: '121.02 MB',
    });

    expect(all(fixture, 'text.donut-total')[0]?.getAttribute('textLength')).toBe('80');
    expect(all(fixture, 'text.donut-total')[0]?.getAttribute('x')).toBe('90');
  });

  it('says a donut of nothing is empty instead of drawing a bare ring', () => {
    const fixture = render(DonutChart, {
      slices: [{ name: 'Mobile', value: 0, color: 'var(--color-hex)' }],
      label: 'Appareils',
    });

    expect(all(fixture, 'circle')).toHaveLength(0);
    expect(all(fixture, 'text').map(words)).toEqual(['admin.chart.empty']);
    // In the middle of the canvas, as the legacy empty chart.
    expect(all(fixture, 'text')[0]?.getAttribute('x')).toBe('380');
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
    expect(cells[0]?.style.background).toBe('var(--color-track)');
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

describe('categorySlices', () => {
  const name = (key: string) => key.toUpperCase();

  it('keeps the colour of each category whatever its rank', () => {
    const slices = categorySlices(
      'resource',
      [
        { name: 'home', value: 10 },
        { name: 'champion', value: 5 },
      ],
      name,
    );

    expect(slices).toEqual([
      { name: 'HOME', value: 10, color: 'var(--color-series-cyan)' },
      { name: 'CHAMPION', value: 5, color: 'var(--color-gold)' },
    ]);
  });

  it('paints a category it does not know in the muted text colour', () => {
    const [slice] = categorySlices('plan', [{ name: 'lifetime', value: 1 }], name);

    expect(slice?.color).toBe('var(--color-text-muted)');
  });
});
