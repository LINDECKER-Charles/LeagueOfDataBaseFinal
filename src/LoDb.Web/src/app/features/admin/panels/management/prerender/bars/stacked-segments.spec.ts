import type { BarSegment } from './stacked-bar';
import { stackedSegments } from './stacked-segments';

function segments(...values: number[]): BarSegment[] {
  return values.map((value, index) => ({
    key: `s${index}`,
    value,
    color: 'var(--color-good)',
    label: `segment ${index}`,
  }));
}

function shares(values: readonly number[], minimum?: number): number[] {
  return stackedSegments(segments(...values), minimum).map((segment) => segment.share);
}

describe('stackedSegments', () => {
  it('shares the bar by value, the shares summing to 1', () => {
    expect(shares([3, 1, 0, 4])).toEqual([0.375, 0.125, 0, 0.5]);
  });

  it('raises a tiny segment to the minimum, the others giving up its room', () => {
    const [written, failed, left] = shares([1_512_300, 270, 613_590], 0.01);

    expect(failed).toBe(0.01);
    expect(written + failed + left).toBeCloseTo(1, 10);
    expect(written / left).toBeCloseTo(1_512_300 / 613_590, 10);
  });

  it('keeps every segment the labels and colours it came with', () => {
    expect(stackedSegments(segments(1))[0]).toEqual({
      key: 's0',
      value: 1,
      color: 'var(--color-good)',
      label: 'segment 0',
      share: 1,
    });
  });

  it('draws nothing of a total of nothing', () => {
    expect(shares([0, 0])).toEqual([0, 0]);
    expect(shares([])).toEqual([]);
  });
});
