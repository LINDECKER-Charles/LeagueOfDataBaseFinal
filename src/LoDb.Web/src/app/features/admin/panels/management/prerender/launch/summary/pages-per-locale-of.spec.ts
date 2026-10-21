import type { PrerenderPatchRow } from '../../../../../../../core/api/generated/models/prerender-patch-row';
import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import { pagesPerLocaleOf } from './pages-per-locale-of';

const ROW = prerenderPatches().patches[0];

// Older patches newest first, each measured at `pages` or not at all.
function rows(...patches: [string, number | null][]): PrerenderPatchRow[] {
  return patches.map(([version, pages]) => ({ ...ROW, version, pagesPerLocale: pages }));
}

describe('pagesPerLocaleOf', () => {
  it('takes the measure of the patch itself', () => {
    expect(pagesPerLocaleOf('16.19.1', rows(['16.19.1', 1_158]))).toEqual({
      pages: 1_158,
      source: 'measured',
      from: null,
    });
  });

  it('takes the median of its season once it has three measures', () => {
    const season = rows(['14.4.1', 400], ['14.3.1', null], ['14.2.1', 420], ['14.1.1', 380]);

    expect(pagesPerLocaleOf('14.3.1', season)).toEqual({
      pages: 400,
      source: 'season',
      from: '14',
    });
  });

  it('takes the nearest patch measured when its season has two measures only', () => {
    const season = rows(['15.1.1', 1_100], ['14.3.1', 410], ['14.2.1', null], ['14.1.1', 390]);

    expect(pagesPerLocaleOf('14.2.1', season)).toEqual({
      pages: 390,
      source: 'neighbour',
      from: '14.1.1',
    });
  });

  it('takes the older of two neighbours as near', () => {
    const tie = rows(['14.3.1', 410], ['14.2.1', null], ['14.1.1', 390]);
    const later = rows(['14.3.1', 410], ['14.2.1', null], ['14.1.1', null]);

    expect(pagesPerLocaleOf('14.2.1', tie).from).toBe('14.1.1');
    expect(pagesPerLocaleOf('14.1.1', later).from).toBe('14.3.1');
  });

  it('falls back on 500 pages without any measure', () => {
    expect(pagesPerLocaleOf('14.2.1', rows(['14.2.1', null], ['14.1.1', null]))).toEqual({
      pages: 500,
      source: 'default',
      from: null,
    });
  });
});
