import type { PrerenderPatchRow } from '../../../../../../core/api/generated/models/prerender-patch-row';
import { prerenderPatches } from '../../../../testing/fixtures/prerender-patches';
import { coverageGroups } from './coverage-groups';

const [WHOLE, UNDER_WAY] = prerenderPatches().patches;
const ABSENT = '-'.repeat(21);

function row(version: string, cells: string): PrerenderPatchRow {
  return { ...WHOLE, version, cells };
}

const PATCHES: readonly PrerenderPatchRow[] = [
  WHOLE,
  UNDER_WAY,
  row('16.10.1', 'w'.repeat(21)),
  row('16.1.1', ABSENT),
  row('15.24.1', `f${ABSENT.slice(1)}`),
  row('15.23.1', 'c'.repeat(21)),
];

function versions(query: string, status: Parameters<typeof coverageGroups>[1]['status']) {
  return coverageGroups(PATCHES, { query, status }).map((group) => ({
    key: group.key,
    rows: group.rows.map((patch) => patch.version),
  }));
}

describe('coverageGroups', () => {
  it('groups the patches by season, newest first, with what each holds', () => {
    const groups = coverageGroups(PATCHES, { query: '', status: 'all' });

    expect(groups.map((group) => [group.key, group.year, group.rows.length])).toEqual([
      ['16', 2026, 4],
      ['15', 2025, 2],
    ]);
    expect(groups.map((group) => [group.complete, group.missing])).toEqual([
      [2, 2],
      [1, 1],
    ]);
  });

  it('filters by text as the launch dialog does', () => {
    expect(versions('16.1', 'all')).toEqual([{ key: '16', rows: ['16.1.1'] }]);
    expect(versions('15', 'all')).toEqual([{ key: '15', rows: ['15.24.1', '15.23.1'] }]);
  });

  it('filters by status, complete taking the failed pages too, and drops empty seasons', () => {
    expect(versions('', 'complete')).toEqual([
      { key: '16', rows: ['16.19.1', '16.10.1'] },
      { key: '15', rows: ['15.23.1'] },
    ]);
    expect(versions('', 'partial')).toEqual([{ key: '16', rows: ['16.18.1'] }]);
    expect(versions('', 'absent')).toEqual([{ key: '16', rows: ['16.1.1'] }]);
    expect(versions('', 'failed')).toEqual([{ key: '15', rows: ['15.24.1'] }]);
  });

  it('has nothing to show when nothing passes', () => {
    expect(coverageGroups(PATCHES, { query: '9', status: 'all' })).toEqual([]);
  });
});
