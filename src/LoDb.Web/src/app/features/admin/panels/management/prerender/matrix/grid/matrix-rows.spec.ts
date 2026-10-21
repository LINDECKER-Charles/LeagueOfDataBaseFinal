import { matrixRows } from './matrix-rows';
import type { MatrixVersion } from './unit-matrix';

const PASS = { mode: 'pass', folds: new Map<string, boolean>() } as const;

// Three seasons in the order of a pass: priorities first, then newest to oldest.
const VERSIONS: MatrixVersion[] = [
  { version: '15.2.1', cells: 'cr.', rank: 0 },
  { version: '16.9.1', cells: 'ccc', rank: 3 },
  { version: '16.10.1', cells: 'cwc', rank: 6 },
  { version: '14.1.1', cells: '..._', rank: 9 },
];

// A pass too long to unfold whole: 40 quiet versions of season 13.
const QUIET = Array.from({ length: 40 }, (_, index) => ({
  version: `13.${index + 1}.1`,
  cells: '...',
  rank: 100 + index,
}));

describe('matrixRows', () => {
  it('groups the versions by season, newest first, each season counted by status', () => {
    const seasons = matrixRows(VERSIONS, PASS);

    expect(seasons.map((season) => season.key)).toEqual(['16', '15', '14']);
    expect(seasons[0].rows.map((row) => row.version.version)).toEqual(['16.10.1', '16.9.1']);
    expect(seasons[0]).toMatchObject({ major: 16, year: 2026, isRanked: false });
    expect(seasons[0].counts).toEqual({
      versions: 2,
      complete: 1,
      failed: 1,
      running: 0,
      pending: 0,
    });
    expect(seasons[1].rows[0]).toMatchObject({ status: 'running', complete: 1, total: 3 });
    expect(seasons[2].rows[0]).toMatchObject({ status: 'pending', total: 3 });
  });

  it('sums up each locale of a season: its share complete, a unit running or failed', () => {
    const [season16, season15] = matrixRows(VERSIONS, PASS);

    expect(season16.columns).toEqual([
      { share: 1, isRunning: false, hasFailed: false },
      { share: 1, isRunning: false, hasFailed: true },
      { share: 1, isRunning: false, hasFailed: false },
    ]);
    expect(season15.columns[1]).toEqual({ share: 0, isRunning: true, hasFailed: false });
  });

  it('opens every season of a pass of 40 versions or fewer', () => {
    expect(matrixRows(VERSIONS, PASS).every((season) => season.isOpen)).toBe(true);
  });

  it('opens only the seasons where a longer pass acts, unless the operator chose', () => {
    const seasons = matrixRows([...VERSIONS, ...QUIET], PASS);
    expect(seasons.map((season) => [season.key, season.isOpen])).toEqual([
      ['16', true],
      ['15', true],
      ['14', false],
      ['13', false],
    ]);

    const folds = new Map([
      ['16', false],
      ['13', true],
    ]);
    const chosen = matrixRows([...VERSIONS, ...QUIET], { ...PASS, folds });
    expect(chosen.map((season) => season.isOpen)).toEqual([false, true, false, true]);
  });

  it('opens a coverage where something is missing or failed', () => {
    const versions = [
      { version: '16.1.1', cells: 'ccc' },
      { version: '15.1.1', cells: 'cp-' },
      ...QUIET.map((quiet) => ({ ...quiet, cells: 'ccc' })),
    ];
    const seasons = matrixRows(versions, { mode: 'coverage', folds: new Map() });

    expect(seasons.map((season) => season.isOpen)).toEqual([false, true, false]);
    expect(seasons[1].rows[0].status).toBe('pending');
    expect(seasons[0].rows[0].status).toBe('complete');
  });

  it('lays the versions out in the order of the pass, in one group always open', () => {
    const [group, ...rest] = matrixRows([...VERSIONS].reverse(), { ...PASS, byRank: true });

    expect(rest).toEqual([]);
    expect(group).toMatchObject({ isRanked: true, isOpen: true, major: null });
    expect(group.rows.map((row) => row.version.version)).toEqual([
      '15.2.1',
      '16.9.1',
      '16.10.1',
      '14.1.1',
    ]);
  });
});
