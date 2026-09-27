import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { configureAdminTestBed } from '../testing/admin-test-bed';
import type { ChartSeries } from './chart-series';
import { PLOT_BOX } from './scale/plot-box';
import { TimeSeriesChart } from './time-series-chart';

const DATES = ['2026-09-01', '2026-09-02', '2026-09-03', '2026-09-04', '2026-09-05'];
const SERIES: readonly ChartSeries[] = [
  { label: 'Vues', color: 'var(--color-gold)', values: [10, 40, 20, 30, 50] },
  { label: 'Dons', color: 'var(--color-hex)', values: [1250, 0, 500, 0, 99], format: 'euros' },
];
// The plot spans these screen X, in SVG units: jsdom lays nothing out, so the chart reads a
// pointer's clientX as is.
const LEFT = PLOT_BOX.padX;
const STEP = PLOT_BOX.plotW / (DATES.length - 1);

function open(dates: readonly string[] = DATES): ComponentFixture<TimeSeriesChart> {
  configureAdminTestBed();
  const fixture = TestBed.createComponent(TimeSeriesChart);
  fixture.componentRef.setInput('dates', dates);
  fixture.componentRef.setInput('series', SERIES);
  fixture.componentRef.setInput('label', 'Trafic');
  fixture.detectChanges();
  return fixture;
}

function query(fixture: ComponentFixture<unknown>, selector: string): HTMLElement | null {
  return (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>(selector);
}

function all(fixture: ComponentFixture<unknown>, selector: string): Element[] {
  return [...(fixture.nativeElement as HTMLElement).querySelectorAll(selector)];
}

function words(element: Element | null | undefined): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

function pointer(fixture: ComponentFixture<unknown>, type: string, clientX: number): void {
  const target = type === 'pointerdown' ? query(fixture, '.ts-hit') : query(fixture, 'svg');
  target?.dispatchEvent(new MouseEvent(type, { clientX, bubbles: true }));
  fixture.detectChanges();
}

function key(fixture: ComponentFixture<unknown>, name: string): void {
  query(fixture, 'svg')?.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true }));
  fixture.detectChanges();
}

function tools(fixture: ComponentFixture<unknown>): HTMLButtonElement[] {
  return all(fixture, 'button.ts-tool') as HTMLButtonElement[];
}

function ticks(fixture: ComponentFixture<unknown>): string[] {
  return all(fixture, 'svg > text.ts-axis')
    .map(words)
    .filter((text) => /^\d\d-\d\d$/.test(text));
}

describe('TimeSeriesChart', () => {
  it('says a chart without dates is empty and offers no tool', () => {
    const fixture = open([]);

    expect(words(query(fixture, 'svg'))).toBe('admin.chart.empty');
    expect(query(fixture, 'svg')?.getAttribute('aria-label')).toBe('Trafic');
    expect(tools(fixture)).toHaveLength(0);
  });

  it('draws a line per series, the first one over its area, dated by month and day', () => {
    const fixture = open();

    const lines = all(fixture, 'polyline.ts-line');
    expect(lines.map((line) => line.getAttribute('stroke'))).toEqual([
      'var(--color-gold)',
      'var(--color-hex)',
    ]);
    expect(all(fixture, 'polygon.ts-area')).toHaveLength(1);
    expect(all(fixture, 'circle[r="2.5"]')).toHaveLength(2 * DATES.length);
    // Four ticks spread over five dates, both ends included.
    expect(ticks(fixture)).toEqual(['09-01', '09-02', '09-04', '09-05']);
    expect(query(fixture, 'svg')?.getAttribute('aria-describedby')).toBe(
      query(fixture, '.sr-only')?.id,
    );
  });

  it('follows the pointer with a crosshair and says every value at that date', () => {
    const fixture = open();

    pointer(fixture, 'pointermove', LEFT + STEP * 2 + 3);

    const tip = query(fixture, '.ts-tip');
    expect(words(tip?.querySelector('.ts-tip-date'))).toBe('2026-09-03');
    expect(all(fixture, '.ts-tip-row').map(words)).toEqual(['Vues 20', 'Dons 5,00 €']);
    expect(query(fixture, 'line.ts-cross')?.getAttribute('x1')).toBe(String(LEFT + STEP * 2));
    expect(all(fixture, 'circle.ts-hover-dot')).toHaveLength(2);

    pointer(fixture, 'pointerleave', 0);
    expect(query(fixture, '.ts-tip')).toBeNull();
  });

  it('flips the tooltip to the left of the crosshair on the right half', () => {
    const fixture = open();

    pointer(fixture, 'pointermove', LEFT);
    expect(query(fixture, '.ts-tip')?.classList).not.toContain('is-flipped');
    pointer(fixture, 'pointermove', LEFT + STEP * 4);
    expect(query(fixture, '.ts-tip')?.classList).toContain('is-flipped');
  });

  it('shows no crosshair over the margins', () => {
    const fixture = open();

    pointer(fixture, 'pointermove', LEFT - 10);

    expect(query(fixture, '.ts-tip')).toBeNull();
  });

  it('moves the crosshair with the arrows and hides it on Escape', () => {
    const fixture = open();

    key(fixture, 'ArrowRight');
    expect(words(query(fixture, '.ts-tip-date'))).toBe('2026-09-02');
    key(fixture, 'ArrowLeft');
    key(fixture, 'ArrowLeft');
    expect(words(query(fixture, '.ts-tip-date'))).toBe('2026-09-01');
    key(fixture, 'Escape');
    expect(query(fixture, '.ts-tip')).toBeNull();
  });

  it('zooms with its buttons and its keys, then resets to the whole series', () => {
    const fixture = open();
    const [zoomOut, zoomIn, reset] = tools(fixture);
    expect(zoomOut?.disabled).toBe(true);
    expect(reset?.disabled).toBe(true);

    zoomIn?.click();
    fixture.detectChanges();
    expect(zoomOut?.disabled).toBe(false);
    expect(query(fixture, 'svg[tabindex]')?.classList).toContain('is-zoomed');

    key(fixture, '+');
    key(fixture, 'Home');
    expect(reset?.disabled).toBe(true);
    expect(query(fixture, 'svg[tabindex]')?.classList).not.toContain('is-zoomed');

    key(fixture, '=');
    reset?.click();
    fixture.detectChanges();
    expect(reset?.disabled).toBe(true);
  });

  it('zooms around the pointer with the wheel, and zooms back out', () => {
    const fixture = open();
    const hit = query(fixture, '.ts-hit');

    hit?.dispatchEvent(new WheelEvent('wheel', { deltaY: -100, clientX: LEFT, bubbles: true }));
    fixture.detectChanges();
    expect(tools(fixture)[2]?.disabled).toBe(false);
    expect(
      query(fixture, 'polyline.ts-line')?.getAttribute('points')?.startsWith(`${LEFT}.0,`),
    ).toBe(true);

    hit?.dispatchEvent(new WheelEvent('wheel', { deltaY: 100, clientX: LEFT, bubbles: true }));
    fixture.detectChanges();
    expect(tools(fixture)[2]?.disabled).toBe(true);
  });

  it('pans a zoomed chart with a drag, and not a chart shown whole', () => {
    const fixture = open();
    const firstX = () => query(fixture, 'polyline.ts-line')?.getAttribute('points')?.split(',')[0];

    pointer(fixture, 'pointerdown', LEFT + STEP);
    pointer(fixture, 'pointermove', LEFT);
    pointer(fixture, 'pointerup', LEFT);
    expect(firstX()).toBe(`${LEFT}.0`);

    for (let step = 0; step < 4; step++) {
      tools(fixture)[1]?.click();
    }
    fixture.detectChanges();
    const zoomed = firstX();
    pointer(fixture, 'pointerdown', LEFT + STEP * 2);
    pointer(fixture, 'pointermove', LEFT + STEP);
    pointer(fixture, 'pointerup', LEFT + STEP);

    expect(Number(firstX())).toBeLessThan(Number(zoomed));
  });
});
