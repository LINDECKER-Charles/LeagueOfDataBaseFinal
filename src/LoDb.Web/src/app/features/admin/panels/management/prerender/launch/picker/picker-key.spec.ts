import type { PrerenderPatchRow } from '../../../../../../../core/api/generated/models/prerender-patch-row';
import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import { type PickerKey, pickerActionOf } from './picker-key';
import { type PickerLine, type PickerRows, pickerRows } from './picker-rows';

const NONE = { selected: new Set<string>(), anchor: null, priority: [], extent: null };

const ROW = prerenderPatches().patches[0];
const VERSIONS = [
  '16.12.1',
  '16.11.1',
  '16.10.1',
  '16.9.1',
  '16.8.1',
  '16.7.1',
  '16.6.1',
  '16.5.1',
  '16.4.1',
  '16.3.1',
  '16.2.1',
  '15.24.1',
  '15.23.1',
];
const ROWS: PrerenderPatchRow[] = VERSIONS.map((version) => ({ ...ROW, version }));
// Season 16 open, 15 folded.
const LIST: PickerRows = pickerRows(
  ROWS,
  { query: '', status: 'all', open: new Set(['16']) },
  NONE,
);

function key(name: string, modifiers: Partial<PickerKey> = {}): PickerKey {
  return { key: name, shiftKey: false, ctrlKey: false, metaKey: false, ...modifiers };
}

function line(lineKey: string): PickerLine {
  const found = LIST.lines.find((candidate) => candidate.key === lineKey);
  if (!found) {
    throw new Error(`No line ${lineKey}.`);
  }
  return found;
}

function press(name: string, on: string, modifiers: Partial<PickerKey> = {}) {
  return pickerActionOf(key(name, modifiers), line(on), LIST);
}

describe('pickerActionOf', () => {
  it('moves to the next and previous lines, seasons included', () => {
    expect(press('ArrowDown', 'season:16')).toEqual({ focus: '16.12.1', action: undefined });
    expect(press('ArrowDown', '16.2.1')).toEqual({ focus: 'season:15', action: undefined });
    expect(press('ArrowUp', '16.12.1')).toEqual({ focus: 'season:16', action: undefined });
    expect(press('ArrowDown', 'season:15')).toEqual({ focus: 'season:15', action: undefined });
  });

  it('extends the range with Shift and an arrow to the next patch, from any line', () => {
    expect(press('ArrowDown', '16.12.1', { shiftKey: true })).toEqual({
      focus: '16.11.1',
      action: { kind: 'extend', version: '16.11.1', rows: LIST.order },
    });
    // No patch above the first one: the focus moves, nothing is ticked.
    expect(press('ArrowUp', '16.12.1', { shiftKey: true })).toEqual({
      focus: 'season:16',
      action: undefined,
    });
    expect(press('ArrowUp', 'season:15', { shiftKey: true })).toEqual({
      focus: '16.2.1',
      action: { kind: 'extend', version: '16.2.1', rows: LIST.order },
    });
    // Season 15 is folded: no patch below the last one of 16.
    expect(press('ArrowDown', '16.2.1', { shiftKey: true })?.action).toBeUndefined();
  });

  it('keeps the range on the focus across an open season, growing then shrinking', () => {
    const open = pickerRows(ROWS, { query: '', status: 'all', open: new Set(['16', '15']) }, NONE);
    const shift = (name: string, on: string) => {
      const from = open.lines.find((candidate) => candidate.key === on);
      return from ? pickerActionOf(key(name, { shiftKey: true }), from, open) : null;
    };

    expect(shift('ArrowDown', '16.2.1')).toEqual({
      focus: '15.24.1',
      action: { kind: 'extend', version: '15.24.1', rows: open.order },
    });
    expect(shift('ArrowUp', '15.24.1')).toEqual({
      focus: '16.2.1',
      action: { kind: 'extend', version: '16.2.1', rows: open.order },
    });
  });

  it('ticks a patch, a season, or extends with Space', () => {
    expect(press(' ', '16.4.1')).toEqual({ action: { kind: 'toggle', version: '16.4.1' } });
    expect(press(' ', '16.4.1', { shiftKey: true })).toEqual({
      action: { kind: 'extend', version: '16.4.1', rows: LIST.order },
    });
    expect(press(' ', 'season:15')).toEqual({
      action: { kind: 'season', versions: ['15.24.1', '15.23.1'] },
    });
    expect(press(' ', 'season:15', { shiftKey: true })).toBeNull();
  });

  it('ticks a patch and folds a season with Enter', () => {
    expect(press('Enter', '16.4.1')).toEqual({ action: { kind: 'toggle', version: '16.4.1' } });
    expect(press('Enter', 'season:16')).toEqual({ fold: { season: '16', isOpen: false } });
    expect(press('Enter', 'season:15')).toEqual({ fold: { season: '15', isOpen: true } });
  });

  it('unfolds, enters, leaves and folds a season with the side arrows', () => {
    expect(press('ArrowRight', 'season:15')).toEqual({ fold: { season: '15', isOpen: true } });
    expect(press('ArrowRight', 'season:16')).toEqual({ focus: '16.12.1' });
    expect(press('ArrowRight', '16.4.1')).toBeNull();
    expect(press('ArrowLeft', '16.4.1')).toEqual({ focus: 'season:16' });
    expect(press('ArrowLeft', 'season:16')).toEqual({ fold: { season: '16', isOpen: false } });
    expect(press('ArrowLeft', 'season:15')).toBeNull();
  });

  it('renders a patch first with P', () => {
    expect(press('p', '16.4.1')).toEqual({ action: { kind: 'priority', version: '16.4.1' } });
    expect(press('P', '16.4.1')).toEqual({ action: { kind: 'priority', version: '16.4.1' } });
    expect(press('p', 'season:16')).toBeNull();
  });

  it('jumps to the ends and by ten lines', () => {
    expect(press('Home', '16.4.1')).toEqual({ focus: 'season:16' });
    expect(press('End', '16.4.1')).toEqual({ focus: 'season:15' });
    expect(press('PageDown', 'season:16')).toEqual({ focus: '16.3.1' });
    expect(press('PageDown', '16.4.1')).toEqual({ focus: 'season:15' });
    expect(press('PageUp', '16.4.1')).toEqual({ focus: 'season:16' });
  });

  it('ticks every visible patch with Ctrl or ⌘ A, undoes with Z', () => {
    const all = { action: { kind: 'set', versions: LIST.order, on: true } };

    expect(press('a', '16.4.1', { ctrlKey: true })).toEqual(all);
    expect(press('a', 'season:16', { metaKey: true })).toEqual(all);
    expect(press('z', '16.4.1', { ctrlKey: true })).toEqual({ undo: true });
    expect(press('c', '16.4.1', { ctrlKey: true })).toBeNull();
  });

  it('goes to the filter with /, searches with digits and points', () => {
    expect(press('/', '16.4.1')).toEqual({ filter: true });
    expect(press('1', '16.4.1')).toEqual({ search: '1' });
    expect(press('.', 'season:16')).toEqual({ search: '.' });
  });

  it('leaves Escape and Tab to the dialog', () => {
    expect(press('Escape', '16.4.1')).toBeNull();
    expect(press('Tab', '16.4.1')).toBeNull();
  });
});
