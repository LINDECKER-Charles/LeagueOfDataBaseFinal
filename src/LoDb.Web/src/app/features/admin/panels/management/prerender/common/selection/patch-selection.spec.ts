import { type PatchSelection, type SelectionAction, selectionAfter } from './patch-selection';

// Newest first, as the list shows them; 15.24.1 opens a season that may be folded.
const ROWS = ['16.3.1', '16.2.1', '16.1.1', '15.24.1', '15.23.1', '15.22.1', '15.21.1'];

function selection(ticked: readonly string[], changes: Partial<PatchSelection> = {}) {
  return { selected: new Set(ticked), anchor: null, priority: [], extent: null, ...changes };
}

function apply(start: PatchSelection, ...actions: SelectionAction[]): PatchSelection {
  return actions.reduce(selectionAfter, start);
}

function ticked(result: PatchSelection): string[] {
  return ROWS.filter((version) => result.selected.has(version));
}

const extend = (version: string): SelectionAction => ({ kind: 'extend', version, rows: ROWS });

describe('selectionAfter', () => {
  it('toggles a row alone, which becomes the anchor', () => {
    const result = apply(selection([]), { kind: 'toggle', version: '16.2.1' });

    expect(ticked(result)).toEqual(['16.2.1']);
    expect(result.anchor).toBe('16.2.1');
  });

  it('extends from the anchor, giving the range the state of the anchor', () => {
    const on = apply(selection([]), { kind: 'toggle', version: '16.3.1' }, extend('16.1.1'));
    expect(ticked(on)).toEqual(['16.3.1', '16.2.1', '16.1.1']);
    expect(on.anchor).toBe('16.3.1');
    expect(on.extent?.to).toBe('16.1.1');

    const off = apply(selection(ROWS), { kind: 'toggle', version: '16.2.1' }, extend('15.24.1'));
    expect(ticked(off)).toEqual(['16.3.1', '15.23.1', '15.22.1', '15.21.1']);
  });

  it('extends through a folded season: the rows are the visible order, folds aside', () => {
    const result = apply(selection([], { anchor: '16.1.1' }), extend('15.22.1'));

    expect(ticked(result)).toEqual([]);
    const ticking = apply(selection(['16.1.1'], { anchor: '16.1.1' }), extend('15.22.1'));
    expect(ticked(ticking)).toEqual(['16.1.1', '15.24.1', '15.23.1', '15.22.1']);
  });

  it('extends as a toggle without an anchor, or with one the rows no longer show', () => {
    const alone = apply(selection([]), extend('16.2.1'));
    expect(ticked(alone)).toEqual(['16.2.1']);
    expect(alone.extent).toBeNull();

    const hidden = apply(selection([], { anchor: '14.1.1' }), extend('16.2.1'));
    expect(ticked(hidden)).toEqual(['16.2.1']);
    expect(hidden.anchor).toBe('16.2.1');
  });

  it('gives the rows a shrinking range leaves their state back', () => {
    const start = selection(['15.23.1'], { anchor: '16.3.1' });
    const grown = apply(
      selectionAfter(start, { kind: 'toggle', version: '16.3.1' }),
      ...['16.2.1', '16.1.1', '15.24.1', '15.23.1', '15.22.1'].map(extend),
    );
    expect(ticked(grown)).toEqual(ROWS.slice(0, 6));

    const shrunk = apply(grown, ...['15.23.1', '15.24.1', '16.1.1'].map(extend));
    expect(ticked(shrunk)).toEqual(['16.3.1', '16.2.1', '16.1.1', '15.23.1']);
    expect(shrunk.extent?.base).toEqual(new Set(['16.3.1', '15.23.1']));
  });

  it('ends the range on any other action', () => {
    const drawing = apply(selection(['16.3.1'], { anchor: '16.3.1' }), extend('16.1.1'));

    expect(selectionAfter(drawing, { kind: 'priority', version: '16.2.1' }).extent).toBeNull();
    expect(selectionAfter(drawing, { kind: 'toggle', version: '15.21.1' }).extent).toBeNull();
  });

  it('ticks a whole season, or unticks it once every row of it is ticked', () => {
    const versions = ['16.3.1', '16.2.1', '16.1.1'];
    const once = apply(selection(['16.2.1']), { kind: 'season', versions });
    expect(ticked(once)).toEqual(versions);

    expect(ticked(selectionAfter(once, { kind: 'season', versions }))).toEqual([]);
  });

  it('sets a range on or off, replaces, inverts and clears', () => {
    const start = selection(['16.3.1', '15.21.1'], { anchor: '16.3.1' });

    const on = selectionAfter(start, { kind: 'set', versions: ['16.2.1', '16.1.1'], on: true });
    expect(ticked(on)).toEqual(['16.3.1', '16.2.1', '16.1.1', '15.21.1']);
    const off = selectionAfter(start, { kind: 'set', versions: ['16.3.1'], on: false });
    expect(ticked(off)).toEqual(['15.21.1']);

    const replaced = selectionAfter(start, { kind: 'replace', versions: ['15.24.1'] });
    expect(ticked(replaced)).toEqual(['15.24.1']);
    expect(replaced.anchor).toBeNull();

    const inverted = selectionAfter(start, { kind: 'invert', rows: ROWS.slice(0, 3) });
    expect(ticked(inverted)).toEqual(['16.2.1', '16.1.1', '15.21.1']);

    expect(selectionAfter(start, { kind: 'clear' })).toEqual(selection([]));
  });

  it('drops from the priorities every version an action unticks', () => {
    const start = selection(ROWS, { anchor: '16.3.1', priority: ['16.3.1', '16.2.1', '15.24.1'] });
    const cases: [SelectionAction, string[]][] = [
      [{ kind: 'toggle', version: '16.2.1' }, ['16.3.1', '15.24.1']],
      [{ kind: 'set', versions: ['15.24.1'], on: false }, ['16.3.1', '16.2.1']],
      [{ kind: 'season', versions: ['15.24.1', '15.23.1'] }, ['16.3.1', '16.2.1']],
      [{ kind: 'invert', rows: ['16.2.1'] }, ['16.3.1', '15.24.1']],
      [{ kind: 'replace', versions: ['16.3.1'] }, ['16.3.1']],
      [{ kind: 'clear' }, []],
    ];
    for (const [action, priority] of cases) {
      expect(selectionAfter(start, action).priority, action.kind).toEqual(priority);
    }

    const unticked = selection(['16.2.1'], { anchor: '16.3.1', priority: ['16.2.1'] });
    expect(selectionAfter(unticked, extend('16.2.1')).priority).toEqual([]);
  });

  it('ticks a version made first, puts it last, and takes it out on a second press', () => {
    const first = selectionAfter(selection([]), { kind: 'priority', version: '16.1.1' });
    expect(ticked(first)).toEqual(['16.1.1']);
    expect(first.priority).toEqual(['16.1.1']);

    const second = selectionAfter(first, { kind: 'priority', version: '16.3.1' });
    expect(second.priority).toEqual(['16.1.1', '16.3.1']);
    const removed = selectionAfter(second, { kind: 'priority', version: '16.1.1' });
    expect(removed.priority).toEqual(['16.3.1']);
    expect(ticked(removed)).toEqual(['16.3.1', '16.1.1']);
  });

  it('refuses a hundred and first priority', () => {
    const hundred = Array.from({ length: 100 }, (_, index) => `1.${index}.1`);
    const full = selection(hundred, { priority: hundred });

    const refused = selectionAfter(full, { kind: 'priority', version: '16.3.1' });
    expect(refused.priority).toEqual(hundred);
    expect(refused.selected.has('16.3.1')).toBe(false);
  });

  it('moves a priority up or down, never past either end', () => {
    const start = selection(ROWS, { priority: ['16.3.1', '16.2.1', '16.1.1'] });

    expect(selectionAfter(start, { kind: 'move', version: '16.2.1', by: -1 }).priority).toEqual([
      '16.2.1',
      '16.3.1',
      '16.1.1',
    ]);
    expect(selectionAfter(start, { kind: 'move', version: '16.1.1', by: 1 }).priority).toEqual(
      start.priority,
    );
  });

  it('ticks a pasted list, which replaces the priorities or follows them, without repeats', () => {
    const start = selection(['16.3.1'], { priority: ['16.3.1'] });
    const versions = ['15.24.1', '16.3.1', '15.24.1'];

    const replaced = selectionAfter(start, { kind: 'paste', versions, append: false });
    expect(replaced.priority).toEqual(['15.24.1', '16.3.1']);
    expect(ticked(replaced)).toEqual(['16.3.1', '15.24.1']);

    const appended = selectionAfter(start, { kind: 'paste', versions, append: true });
    expect(appended.priority).toEqual(['16.3.1', '15.24.1']);
  });
});
