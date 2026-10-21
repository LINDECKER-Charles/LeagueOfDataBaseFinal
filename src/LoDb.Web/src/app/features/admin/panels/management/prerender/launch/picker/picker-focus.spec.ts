import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import { PickerFocus } from './picker-focus';
import { pickerRows } from './picker-rows';

const NONE = { selected: new Set<string>(), anchor: null, priority: [], extent: null };

const ROW = prerenderPatches().patches[0];
const ROWS = ['16.12.1', '16.2.1', '16.1.1', '15.24.1', '14.1.1'].map((version) => ({
  ...ROW,
  version,
}));
const LINES = pickerRows(
  ROWS,
  {
    query: '',
    status: 'all',
    open: new Set(['16', '15', '14']),
  },
  NONE,
).lines;

describe('PickerFocus', () => {
  it('gives the tab stop to the first line until one is chosen', () => {
    const focus = new PickerFocus();

    expect(focus.keyIn(LINES)).toBe('season:16');
    focus.moveTo('16.2.1');
    expect(focus.keyIn(LINES)).toBe('16.2.1');
    expect(focus.keyIn([])).toBeNull();
  });

  it('gives it back to the first line once its own is no longer shown', () => {
    const focus = new PickerFocus();
    focus.moveTo('15.24.1');

    const folded = pickerRows(
      ROWS,
      { query: '', status: 'all', open: new Set(['16']) },
      NONE,
    ).lines;
    expect(focus.keyIn(folded)).toBe('season:16');
  });

  it('searches the versions typed within 600 ms as one', () => {
    const focus = new PickerFocus();

    expect(focus.search('1', LINES, 1_000)).toBe('16.12.1');
    expect(focus.search('5', LINES, 1_300)).toBe('15.24.1');
    expect(focus.keyIn(LINES)).toBe('15.24.1');
  });

  it('starts a new search past 600 ms', () => {
    const focus = new PickerFocus();
    focus.search('1', LINES, 1_000);
    focus.search('6', LINES, 1_200);

    expect(focus.search('1', LINES, 1_900)).toBe('16.12.1');
    expect(focus.search('4', LINES, 2_000)).toBe('14.1.1');
  });

  it('stays put when no version starts with the search', () => {
    const focus = new PickerFocus();
    focus.moveTo('16.1.1');

    expect(focus.search('9', LINES, 1_000)).toBeNull();
    expect(focus.keyIn(LINES)).toBe('16.1.1');
  });
});
