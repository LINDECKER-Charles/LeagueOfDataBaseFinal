import { type PatchSelection, type SelectionAction, selectionAfter } from './patch-selection';
import { SelectionUndo } from './selection-undo';

const ROWS = ['16.3.1', '16.2.1', '16.1.1', '15.24.1'];
const START: PatchSelection = { selected: new Set(), anchor: null, priority: [], extent: null };

// Applies each action as the list does: the undo learns the state before it.
function play(actions: readonly SelectionAction[]) {
  let selection = START;
  let undo = SelectionUndo.empty();
  for (const action of actions) {
    undo = undo.after(selection, action);
    selection = selectionAfter(selection, action);
  }
  return { selection, undo };
}

describe('SelectionUndo', () => {
  it('stacks a grouped action, then gives the state before it back', () => {
    const { undo } = play([{ kind: 'set', versions: ROWS, on: true }]);

    const step = undo.undo();
    expect(step?.selection).toBe(START);
    expect(step?.rest.size).toBe(0);
    expect(step?.rest.undo()).toBeNull();
  });

  it('leaves the toggles and the priorities out: doing them again undoes them', () => {
    const { undo } = play([
      { kind: 'toggle', version: '16.3.1' },
      { kind: 'priority', version: '16.2.1' },
      { kind: 'move', version: '16.2.1', by: 1 },
    ]);

    expect(undo.size).toBe(0);
  });

  it('keeps the 20 latest states', () => {
    const actions = Array.from({ length: 25 }, (_, index): SelectionAction => ({
      kind: 'set',
      versions: [`1.${index}.1`],
      on: true,
    }));
    const { undo } = play(actions);

    expect(undo.size).toBe(20);
    expect(undo.undo()?.selection.selected.size).toBe(24);
  });

  it('stacks a run of extensions of one range once, the state before the first', () => {
    const extend = (version: string): SelectionAction => ({ kind: 'extend', version, rows: ROWS });
    const { undo } = play([
      { kind: 'toggle', version: '16.3.1' },
      extend('16.2.1'),
      extend('16.1.1'),
      extend('15.24.1'),
    ]);

    expect(undo.size).toBe(1);
    expect([...(undo.undo()?.selection.selected ?? [])]).toEqual(['16.3.1']);
  });
});
