import type { WarmUpProgress } from '../../api/generated/models/warm-up-progress';
import type { WarmUpStage } from '../../api/generated/models/warm-up-stage';
import { percentOf } from './percent-of';
import { preparingFrame } from './preparing-frame';
import { resourcesOf } from './resources-of';

const isVersion = (segment: string): boolean => /^\d+(?:\.\d+)+$/.test(segment);

function frame(stage: WarmUpStage, settled: number, total: number): WarmUpProgress {
  return { ...preparingFrame(['champions']), stage, settled, total };
}

describe('resourcesOf', () => {
  it('warms every list for the home page, which previews each', () => {
    const all = ['champions', 'items', 'runes', 'summoners'];

    expect(resourcesOf('/fr/', isVersion)).toEqual(all);
    expect(resourcesOf('/fr?version=15.14.1&lang=fr_FR', isVersion)).toEqual(all);
  });

  it('warms the list of a list page, pinned or following the latest', () => {
    expect(resourcesOf('/fr/15.14.1/items?page=2', isVersion)).toEqual(['items']);
    expect(resourcesOf('/en/summoners?lang=en_GB', isVersion)).toEqual(['summoners']);
  });

  it('warms no list for a detail page, which resolves its images, nor for other pages', () => {
    expect(resourcesOf('/fr/15.14.1/champions/Ahri', isVersion)).toEqual([]);
    expect(resourcesOf('/fr/about', isVersion)).toEqual([]);
    expect(resourcesOf('/b/k3y?lang=fr_FR', isVersion)).toEqual([]);
  });
});

describe('percentOf', () => {
  it('counts nothing while the datasets are prepared, nor after a failure', () => {
    expect(percentOf(frame('preparing', 0, 0))).toBe(0);
    expect(percentOf(frame('failed', 0, 0))).toBe(0);
  });

  it('rounds the share of the images fetched down, 100% kept for the end', () => {
    expect(percentOf(frame('images', 1, 3))).toBe(33);
    expect(percentOf(frame('images', 200, 200))).toBe(99);
  });

  it('is full once done, or at once when there is nothing to fetch', () => {
    expect(percentOf(frame('done', 3, 3))).toBe(100);
    expect(percentOf(frame('images', 0, 0))).toBe(100);
  });
});
