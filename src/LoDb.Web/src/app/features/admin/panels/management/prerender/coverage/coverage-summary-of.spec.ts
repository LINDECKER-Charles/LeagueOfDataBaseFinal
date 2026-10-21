import { prerenderPatches } from '../../../../testing/fixtures/prerender-patches';
import { coverageSummaryOf } from './coverage-summary-of';

const [WHOLE] = prerenderPatches().patches;
const ABSENT = '-'.repeat(21);

describe('coverageSummaryOf', () => {
  it('counts the patches by status and takes the share of the units from the API', () => {
    expect(coverageSummaryOf(prerenderPatches())).toEqual({
      complete: 1,
      partial: 1,
      absent: 0,
      failed: 0,
      total: 2,
      share: 8_274 / 8_442,
      gapVersions: ['16.18.1'],
      seasons: [
        { key: '16', major: 16, year: 2026, complete: 1, partial: 1, failed: 0, absent: 0 },
      ],
    });
  });

  it('draws the failed pages with the partial patches, season by season', () => {
    const report = prerenderPatches({
      patches: [
        { ...WHOLE, version: '16.2.1', cells: 'w'.repeat(21) },
        { ...WHOLE, version: '15.1.1', cells: `f${ABSENT.slice(1)}` },
        { ...WHOLE, version: '15.0.1', cells: ABSENT },
      ],
    });
    const summary = coverageSummaryOf(report);

    expect([summary.complete, summary.partial, summary.failed, summary.absent]).toEqual([
      1, 0, 1, 1,
    ]);
    expect(summary.gapVersions).toEqual(['15.1.1', '15.0.1']);
    expect(summary.seasons.map((season) => [season.key, season.partial, season.failed])).toEqual([
      ['16', 1, 0],
      ['15', 0, 1],
    ]);
  });

  it('has no share while no unit is expected', () => {
    const coverage = { ...prerenderPatches().coverage, expectedUnits: 0, completeUnits: 0 };
    const summary = coverageSummaryOf(prerenderPatches({ patches: [], coverage }));

    expect(summary.share).toBeNull();
    expect(summary.total).toBe(0);
  });
});
