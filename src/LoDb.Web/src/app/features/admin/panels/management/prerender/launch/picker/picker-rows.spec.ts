import type { PrerenderPatchRow } from '../../../../../../../core/api/generated/models/prerender-patch-row';
import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import type { PatchSelection } from '../../common/selection/patch-selection';
import { type PickerFilter, pickerRows } from './picker-rows';

const ROW = prerenderPatches().patches[0];
const COMPLETE = 'ccc';
const ABSENT = '---';

// Newest first: two patches of 16, two of 15, one of 14; cells over three locales.
const ROWS: PrerenderPatchRow[] = [
  { ...ROW, version: '16.2.1', cells: COMPLETE },
  { ...ROW, version: '16.1.1', cells: 'cp-' },
  { ...ROW, version: '15.24.1', cells: ABSENT },
  { ...ROW, version: '15.10.1', cells: 'cwc' },
  { ...ROW, version: '14.1.1', cells: 'fff' },
];

const NONE: PatchSelection = { selected: new Set(), anchor: null, priority: [], extent: null };

function filter(changes: Partial<PickerFilter> = {}): PickerFilter {
  return { query: '', status: 'all', open: new Set(['16', '15', '14']), ...changes };
}

function keys(changes: Partial<PickerFilter> = {}): string[] {
  return pickerRows(ROWS, filter(changes), NONE).lines.map((line) => line.key);
}

describe('pickerRows', () => {
  it('groups the patches by season, newest first', () => {
    expect(keys()).toEqual([
      'season:16',
      '16.2.1',
      '16.1.1',
      'season:15',
      '15.24.1',
      '15.10.1',
      'season:14',
      '14.1.1',
    ]);
  });

  it('sums a season by locale: held by each, by none, by some', () => {
    const [sixteen] = pickerRows(ROWS, filter(), NONE).lines;

    expect(sixteen).toMatchObject({ kind: 'season', cells: 'cpp', isOpen: true });
    expect(sixteen.kind === 'season' && sixteen.season.year).toBe(2026);
    const fourteen = pickerRows(ROWS, filter(), NONE).lines.find(
      (line) => line.key === 'season:14',
    );
    expect(fourteen).toMatchObject({ cells: '---' });
  });

  it('filters by text, a season no patch passes left out', () => {
    expect(keys({ query: '15.10' })).toEqual(['season:15', '15.10.1']);
    expect(keys({ query: '14, 16.2' })).toEqual(['season:16', '16.2.1', 'season:14', '14.1.1']);
  });

  it('filters by status, partial taking the ones complete with failures', () => {
    expect(keys({ status: 'partial' })).toEqual(['season:16', '16.1.1', 'season:15', '15.10.1']);
    expect(keys({ status: 'absent' })).toEqual(['season:15', '15.24.1']);
    expect(keys({ status: 'failed' })).toEqual(['season:14', '14.1.1']);
    expect(keys({ status: 'complete' })).toEqual(['season:16', '16.2.1']);
  });

  it('hides the patches of a folded season, which still count in a range', () => {
    const rows = pickerRows(ROWS, filter({ open: new Set(['16']) }), NONE);

    expect(rows.lines.map((line) => line.key)).toEqual([
      'season:16',
      '16.2.1',
      '16.1.1',
      'season:15',
      'season:14',
    ]);
    expect(rows.order).toEqual(['16.2.1', '16.1.1', '15.24.1', '15.10.1', '14.1.1']);
    expect(rows.lines[3]).toMatchObject({ isOpen: false, versions: ['15.24.1', '15.10.1'] });
  });

  it('ticks with a season box only the patches the filters let through', () => {
    const [fifteen] = pickerRows(ROWS, filter({ query: '15.24' }), NONE).lines;

    expect(fifteen).toMatchObject({ key: 'season:15', versions: ['15.24.1'] });
  });

  it('says what the selection ticks of each line, and the rank of the first ones', () => {
    const selection: PatchSelection = {
      selected: new Set(['16.1.1', '15.24.1', '15.10.1']),
      anchor: null,
      priority: ['15.10.1', '16.1.1'],
      extent: null,
    };
    const lines = pickerRows(ROWS, filter(), selection).lines;
    const of = (key: string) => lines.find((line) => line.key === key);

    expect(of('season:16')).toMatchObject({ ticked: 1 });
    expect(of('season:15')).toMatchObject({ ticked: 2 });
    expect(of('16.2.1')).toMatchObject({ isTicked: false, rank: 0 });
    expect(of('16.1.1')).toMatchObject({ isTicked: true, rank: 2 });
    expect(of('15.10.1')).toMatchObject({ isTicked: true, rank: 1 });
  });
});
