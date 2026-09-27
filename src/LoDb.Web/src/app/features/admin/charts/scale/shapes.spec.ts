import { donutArcs } from './donut-arcs';
import { heatColor } from './heat-color';
import { polylinePoints } from './polyline-points';
import { sparklineShape } from './sparkline-shape';

const CIRCUMFERENCE = 2 * Math.PI * 54;

function dashOf(dasharray: string): number {
  return Number(dasharray.split(' ')[0]);
}

describe('heatColor', () => {
  it('paints an empty cell, or a grid without visits, with the track', () => {
    expect(heatColor(0, 10)).toBe('var(--color-track)');
    expect(heatColor(4, 0)).toBe('var(--color-track)');
  });

  it('mixes the cyan token, fully opaque on the busiest cell', () => {
    expect(heatColor(10, 10)).toBe('color-mix(in srgb, var(--color-hex) 100%, transparent)');
  });

  it('keeps a single visit visible and lifts the low end of the ramp', () => {
    const faint = heatColor(1, 1000);
    const share = Number(/(\d+(\.\d+)?)%/.exec(faint)?.[1]);

    expect(share).toBeGreaterThan(10);
    expect(share).toBeLessThan(15);
    expect(heatColor(250, 1000)).toBe('color-mix(in srgb, var(--color-hex) 49.2%, transparent)');
  });
});

describe('sparklineShape', () => {
  it('draws nothing under two values', () => {
    expect(sparklineShape([])).toBeNull();
    expect(sparklineShape([4])).toBeNull();
  });

  it('spans the whole width, the lowest value at the bottom and the highest at the top', () => {
    expect(sparklineShape([0, 5, 10])?.line).toBe('0.0,28.0 60.0,15.0 120.0,2.0');
  });

  it('lays a flat series along the bottom and closes the area on the baseline', () => {
    const shape = sparklineShape([3, 3]);

    expect(shape?.line).toBe('0.0,28.0 120.0,28.0');
    expect(shape?.area).toBe('0.0,30.0 0.0,28.0 120.0,28.0 120.0,30.0');
    expect(shape?.viewBox).toBe('0 0 120 30');
  });
});

describe('donutArcs', () => {
  it('draws nothing for a whole of nothing', () => {
    expect(donutArcs([])).toEqual([]);
    expect(donutArcs([{ name: 'a', value: 0, color: 'var(--color-hex)' }])).toEqual([]);
  });

  it('gives each slice its share of the ring, one after the other', () => {
    const [first, second] = donutArcs([
      { name: 'Desktop', value: 3, color: 'var(--color-gold)' },
      { name: 'Mobile', value: 1, color: 'var(--color-hex)' },
    ]);

    expect(first.pct).toBe(75);
    expect(second.pct).toBe(25);
    expect(dashOf(first.dasharray)).toBeCloseTo(0.75 * CIRCUMFERENCE - 2, 1);
    expect(first.dashoffset).toBeCloseTo(0);
    expect(second.dashoffset).toBeCloseTo(-0.75 * CIRCUMFERENCE);
  });

  it('keeps a tiny slice from drawing a negative dash', () => {
    const arcs = donutArcs([
      { name: 'big', value: 100_000, color: 'var(--color-gold)' },
      { name: 'tiny', value: 1, color: 'var(--color-hex)' },
    ]);

    expect(dashOf(arcs[1].dasharray)).toBe(0);
  });
});

describe('polylinePoints', () => {
  it('writes the points to a tenth of a unit', () => {
    expect(
      polylinePoints([
        [1, 2.25],
        [3.333, 4],
      ]),
    ).toBe('1.0,2.3 3.3,4.0');
  });
});
