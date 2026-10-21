import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderVersionProgress } from '../../../../../../../core/api/generated/models/prerender-version-progress';
import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { failuresPreset } from './failures-preset';

// The pass wrote epoch 2, still the served one.
const SERVED = 2;
const PASS: PrerenderPassView = {
  ...prerenderProgress().pass,
  state: 'completed',
  scope: {
    versionCount: 3,
    newestListed: '16.18.1',
    oldestListed: '15.2.1',
    locales: ['en', 'fr', 'zh-hans'],
    pageTypes: ['items'],
    priority: ['16.18.1', '15.2.1'],
    skipComplete: false,
  },
};

function version(name: string, cells: string, pagesTotal: number): PrerenderVersionProgress {
  return {
    ...prerenderProgress().versions[0],
    version: name,
    cells,
    pagesTotal,
  };
}

// 16.18.1 lost two pages in fr; 16.12.1 is clean; 15.2.1 failed for good in zh-hans.
const VERSIONS = [
  version('16.18.1', 'cwc', 3_000),
  version('16.12.1', 'ccc', 2_400),
  version('15.2.1', 'ccf', 1_000),
];

describe('failuresPreset', () => {
  it('relaunches the versions a cell of which is not complete', () => {
    expect(failuresPreset(PASS, VERSIONS, SERVED)?.preset.versions).toEqual(['16.18.1', '15.2.1']);
  });

  it('keeps the locales, the types and the epoch of the pass, leaving out what is held', () => {
    expect(failuresPreset(PASS, VERSIONS, SERVED)?.preset).toEqual({
      origin: 'failures',
      versions: ['16.18.1', '15.2.1'],
      priority: ['16.18.1', '15.2.1'],
      locales: ['en', 'fr', 'zh-hans'],
      pageTypes: ['items'],
      epoch: 2,
      skipComplete: true,
      fromPass: 12,
    });
  });

  it("goes to the served epoch once the pass's epoch was retired", () => {
    expect(failuresPreset(PASS, VERSIONS, 3)?.preset.epoch).toBe(3);
  });

  it('counts the units it renders again and their pages, each at its version mean', () => {
    const preset = failuresPreset(PASS, VERSIONS, SERVED);

    expect(preset?.units).toBe(2);
    // One unit of 16.18.1 (3 000 / 3) and one of 15.2.1 (1 000 / 3, its f counted listed).
    expect(preset?.pages).toBe(1_333);
    expect(preset?.hasGaps).toBe(false);
  });

  it('takes the gaps of a cancelled pass too, at the mean of the pass when never listed', () => {
    const cancelled = { ...PASS, state: 'cancelled' };
    const preset = failuresPreset(
      cancelled,
      [
        version('16.18.1', 'cci', 3_000),
        version('16.12.1', 'z..', 1_000),
        version('15.2.1', '...', 0),
      ],
      SERVED,
    );

    expect(preset?.units).toBe(7);
    expect(preset?.hasGaps).toBe(true);
    // i: 3 000 over 3 listed; z and its two dots: 3 × 1 000, its z alone listed; the three
    // dots of 15.2.1, never listed, at the mean of the pass: 4 000 over 4 listed.
    expect(preset?.pages).toBe(7_000);
  });

  it('leaves the gaps of a pass still running to the pass', () => {
    const running = { ...PASS, state: 'running' };

    expect(failuresPreset(running, [version('16.18.1', 'cr.', 1_000)], SERVED)).toBeNull();
  });

  it('offers nothing when every unit completed cleanly', () => {
    expect(failuresPreset(PASS, [version('16.12.1', 'ccc', 2_400)], SERVED)).toBeNull();
  });
});
