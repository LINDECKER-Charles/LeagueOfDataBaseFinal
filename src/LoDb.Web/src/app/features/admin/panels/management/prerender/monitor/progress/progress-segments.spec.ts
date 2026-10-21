import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { progressSegments } from './progress-segments';

const PASS = prerenderProgress().pass;

function values(pass: PrerenderPassView, shown?: number): Record<string, number> {
  const parts = progressSegments(pass, shown).parts;
  return Object.fromEntries(parts.map((part) => [part.key, part.value]));
}

describe('progressSegments', () => {
  it('counts pages written, skipped, failed and left, the total estimated', () => {
    const figures = progressSegments(PASS);

    expect(values(PASS)).toEqual({
      written: 1_512_300,
      skipped: 19_840,
      failed: 270,
      left: 613_590,
    });
    expect(figures.done).toBe(1_532_410);
    expect(figures.total).toBe(2_146_000);
    expect(figures.share).toBeCloseTo(0.714, 3);
    expect(figures.isEstimate).toBe(true);
    expect(figures.isUnits).toBe(false);
  });

  it('is exact once no unit is to come', () => {
    const pass = { ...PASS, units: { ...PASS.units, pending: 0 } };

    expect(progressSegments(pass).isEstimate).toBe(false);
  });

  it('counts units before any lists its pages', () => {
    const pass = { ...PASS, pagesLeft: null, units: { ...PASS.units, completed: 0, failed: 2 } };
    const figures = progressSegments(pass);

    expect(values(pass)).toEqual({ completed: 0, failed: 2, left: 4_450 });
    expect(figures.isUnits).toBe(true);
    expect(figures.share).toBeCloseTo(2 / 4_452);
  });

  it('draws less left when the share on screen is ahead of the figure', () => {
    const left = values(PASS, 0.8)['left'];

    expect(1_532_410 / (1_532_410 + left)).toBeCloseTo(0.8);
    expect(values(PASS, 0.5)['left']).toBe(613_590);
    expect(values(PASS, 1)['left']).toBe(0);
  });
});
