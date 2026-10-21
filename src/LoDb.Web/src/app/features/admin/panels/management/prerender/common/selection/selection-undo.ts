import type { PatchSelection, SelectionAction } from './patch-selection';

// Enough to step back through a session of selection without holding every state.
const MAX_STATES = 20;
// The actions that change many rows at once: a toggle, a priority or a move is undone by
// doing it again.
const GROUPED: ReadonlySet<SelectionAction['kind']> = new Set([
  'extend',
  'season',
  'set',
  'replace',
  'invert',
  'clear',
  'paste',
]);

/** A selection to go back to, and the undo left once it is taken. */
export interface UndoStep {
  readonly selection: PatchSelection;
  readonly rest: SelectionUndo;
}

/**
 * The states a list of patches can go back to (« ↶ Annuler », Ctrl/⌘+Z), 20 at most, the
 * latest last. Immutable: each action gives a new undo. A run of extensions of one range
 * stacks once, the state before its first extension.
 */
export class SelectionUndo {
  private constructor(private readonly states: readonly PatchSelection[]) {}

  /** An undo with nothing to go back to. */
  static empty(): SelectionUndo {
    return new SelectionUndo([]);
  }

  /** How many states it can go back through. */
  get size(): number {
    return this.states.length;
  }

  /** The undo once `action` moved the selection from `before`. */
  after(before: PatchSelection, action: SelectionAction): SelectionUndo {
    const isRangeGoingOn = action.kind === 'extend' && before.extent !== null;
    if (!GROUPED.has(action.kind) || isRangeGoingOn) {
      return this;
    }
    return new SelectionUndo([...this.states, before].slice(-MAX_STATES));
  }

  /** The latest state and the undo without it; null when there is nothing to undo. */
  undo(): UndoStep | null {
    const selection = this.states.at(-1);
    return selection ? { selection, rest: new SelectionUndo(this.states.slice(0, -1)) } : null;
  }
}
