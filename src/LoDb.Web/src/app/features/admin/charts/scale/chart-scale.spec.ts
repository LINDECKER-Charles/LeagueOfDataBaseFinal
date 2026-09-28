import { ChartScale } from './chart-scale';
import { PLOT_BOX } from './plot-box';

// The legacy canvas: the plot runs from x = 34 to x = 726, from y = 16 down to y = 214.
const LEFT = 34;
const RIGHT = 726;
const TOP = 16;
const BASELINE = 214;

describe('ChartScale', () => {
  it('lays the first and the last point on the edges of the plot', () => {
    const scale = ChartScale.of(11);

    expect(scale.modelX(0)).toBe(LEFT);
    expect(scale.modelX(10)).toBe(RIGHT);
    expect(scale.modelX(5)).toBeCloseTo(380);
  });

  it('centres a lone point, which would otherwise read as a cut series', () => {
    expect(ChartScale.of(1).modelX(0)).toBe(LEFT + PLOT_BOX.plotW / 2);
  });

  it('draws a series as is before any zoom', () => {
    const scale = ChartScale.of(11);

    expect(scale.zoomed).toBe(false);
    expect(scale.toScreen(123)).toBe(123);
    expect(scale.screenX(3)).toBe(scale.modelX(3));
  });

  it('turns a screen X back into the model at any zoom', () => {
    const scale = ChartScale.of(11).zoomAt(200, 3).panBy(-40);

    expect(scale.toModel(scale.toScreen(123.4))).toBeCloseTo(123.4);
  });

  it('keeps the point under the pointer still while zooming', () => {
    const scale = ChartScale.of(11).zoomAt(380, 2);

    expect(scale.scale).toBe(2);
    expect(scale.zoomed).toBe(true);
    expect(scale.screenX(5)).toBeCloseTo(380);
  });

  it('zooms in 24 times at most', () => {
    expect(ChartScale.of(11).zoomAt(380, 100).scale).toBe(24);
  });

  it('never zooms out past the whole series', () => {
    const scale = ChartScale.of(11).zoomAt(380, 2).zoomAt(380, 0.1);

    expect(scale.scale).toBe(1);
    expect(scale.offset).toBe(0);
  });

  it('does not pan a series shown whole', () => {
    expect(ChartScale.of(11).panBy(50).offset).toBe(0);
  });

  it('never pans the start of the series past the left edge', () => {
    const scale = ChartScale.of(11).zoomAt(380, 2).panBy(1000);

    expect(scale.offset).toBe(0);
    expect(scale.screenX(0)).toBe(LEFT);
  });

  it('never pans the end of the series past the right edge', () => {
    const scale = ChartScale.of(11).zoomAt(380, 2).panBy(-1000);

    expect(scale.offset).toBe(-PLOT_BOX.plotW);
    expect(scale.screenX(10)).toBeCloseTo(RIGHT);
  });

  it('finds the point nearest to a screen X', () => {
    expect(ChartScale.of(11).indexAt(400)).toBe(5);
  });

  it('clamps a screen X outside the plot to the series', () => {
    const scale = ChartScale.of(11);

    expect(scale.indexAt(-100)).toBe(0);
    expect(scale.indexAt(2000)).toBe(10);
  });

  it('answers the only point of a series shorter than two', () => {
    expect(ChartScale.of(1).indexAt(600)).toBe(0);
    expect(ChartScale.of(0).indexAt(600)).toBe(0);
  });

  it('tells the plot from its margins', () => {
    const scale = ChartScale.of(11);

    expect(scale.contains(LEFT)).toBe(true);
    expect(scale.contains(RIGHT)).toBe(true);
    expect(scale.contains(LEFT - 1)).toBe(false);
    expect(scale.contains(RIGHT + 1)).toBe(false);
  });

  it('shows every point of a series not zoomed', () => {
    expect(ChartScale.of(11).visibleRange()).toEqual([0, 10]);
  });

  it('narrows the visible points as it zooms', () => {
    expect(ChartScale.of(5).zoomAt(380, 2).visibleRange()).toEqual([1, 3]);
  });

  it('leaves a view alone when the point to reveal is already shown', () => {
    const scale = ChartScale.of(5).zoomAt(380, 2);

    expect(scale.reveal(2)).toBe(scale);
  });

  it('pans a hidden point just onto the edge of the plot', () => {
    const scale = ChartScale.of(5).zoomAt(380, 2);

    expect(scale.contains(scale.screenX(0))).toBe(false);
    expect(scale.reveal(0).screenX(0)).toBeCloseTo(LEFT);
  });

  it('spreads four ticks over the visible range, both ends included', () => {
    expect(ChartScale.of(11).ticks()).toEqual([0, 3, 7, 10]);
  });

  it('gives one tick per point when the range is short, and one for a lone point', () => {
    expect(ChartScale.of(3).ticks()).toEqual([0, 1, 2]);
    expect(ChartScale.of(1).ticks()).toEqual([0]);
  });

  it('puts zero on the baseline and the maximum on the top of the plot', () => {
    const scale = ChartScale.of(11);

    expect(scale.y(0, 100)).toBe(BASELINE);
    expect(scale.y(100, 100)).toBe(TOP);
    expect(scale.y(50, 100)).toBe((TOP + BASELINE) / 2);
    expect(scale.y(5, 0)).toBe(BASELINE);
  });

  it('comes back to the whole series on reset', () => {
    const scale = ChartScale.of(11).zoomAt(200, 4).panBy(-30).reset();

    expect(scale.scale).toBe(1);
    expect(scale.offset).toBe(0);
    expect(scale.count).toBe(11);
  });
});
