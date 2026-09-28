import { DragSession } from './drag-session';
import { insertionIndex } from './insertion-index';

interface Source {
  readonly kind: string;
  readonly index: number;
}

interface Target {
  readonly insert: number;
}

function session() {
  const onCommit = vi.fn<(source: Source, target: Target) => void>();
  const onCancel = vi.fn<() => void>();
  return {
    drag: new DragSession<Source, Target>({ onCommit, onCancel }, document),
    onCommit,
    onCancel,
  };
}

function pressEscape(): void {
  document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
}

describe('DragSession', () => {
  it('commits the source and the target of a drop, then clears', () => {
    const { drag, onCommit, onCancel } = session();

    drag.start({ kind: 'step', index: 1 });
    expect(drag.dragged()).toEqual({ kind: 'step', index: 1 });
    drag.drop({ insert: 3 });

    expect(onCommit).toHaveBeenCalledExactlyOnceWith({ kind: 'step', index: 1 }, { insert: 3 });
    expect(onCancel).not.toHaveBeenCalled();
    expect(drag.dragged()).toBeNull();
  });

  it('ignores a drop no drag started', () => {
    const { drag, onCommit, onCancel } = session();

    drag.drop({ insert: 0 });

    expect(onCommit).not.toHaveBeenCalled();
    expect(onCancel).not.toHaveBeenCalled();
  });

  it('cancels a release outside any list', () => {
    const { drag, onCommit, onCancel } = session();

    drag.start({ kind: 'item', index: 0 });
    drag.drop(null);

    expect(onCancel).toHaveBeenCalledOnce();
    expect(onCommit).not.toHaveBeenCalled();
    expect(drag.dragged()).toBeNull();
  });

  it('stays silent when cancelled after a drop that landed', () => {
    const { drag, onCancel } = session();

    drag.start({ kind: 'item', index: 0 });
    drag.drop({ insert: 1 });
    drag.cancel();

    expect(onCancel).not.toHaveBeenCalled();
  });

  it('gives the drag up on Escape, then ignores its drop', () => {
    const { drag, onCommit, onCancel } = session();

    drag.start({ kind: 'step', index: 0 });
    pressEscape();
    drag.drop({ insert: 2 });

    expect(onCancel).toHaveBeenCalledOnce();
    expect(onCommit).not.toHaveBeenCalled();
    expect(drag.dragged()).toBeNull();
  });

  it('listens to Escape only while a drag lasts', () => {
    const { drag, onCancel } = session();

    drag.start({ kind: 'step', index: 0 });
    pressEscape();
    pressEscape();
    expect(onCancel).toHaveBeenCalledOnce();

    drag.start({ kind: 'step', index: 0 });
    drag.drop({ insert: 1 });
    pressEscape();
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('removes its Escape listener when disposed, silently', () => {
    const { drag, onCancel } = session();

    drag.start({ kind: 'step', index: 0 });
    drag.dispose();
    pressEscape();

    expect(onCancel).not.toHaveBeenCalled();
    expect(drag.dragged()).toBeNull();
  });
});

describe('insertionIndex', () => {
  it('reads a move down its own list as an insertion after the resting place', () => {
    expect(insertionIndex({ previousIndex: 0, currentIndex: 2, sameList: true })).toBe(3);
  });

  it('reads a move up, or no move, as the resting place', () => {
    expect(insertionIndex({ previousIndex: 2, currentIndex: 0, sameList: true })).toBe(0);
    expect(insertionIndex({ previousIndex: 1, currentIndex: 1, sameList: true })).toBe(1);
  });

  it('reads a move to another list as the resting place', () => {
    expect(insertionIndex({ previousIndex: 0, currentIndex: 2, sameList: false })).toBe(2);
  });
});
