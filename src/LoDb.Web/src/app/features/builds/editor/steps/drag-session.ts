import { signal } from '@angular/core';

interface DragCallbacks<S, T> {
  /** A drop that lands: what was dragged, and where. */
  readonly onCommit: (source: S, target: T) => void;
  /** A drag given up: Escape, or a release outside any list. */
  readonly onCancel: () => void;
}

const ESCAPE = 'Escape';

/**
 * One drag of the purchase order at a time, between the CDK's events and the moves: the CDK
 * moves the elements, this session decides whether the drop counts. Escape gives the drag
 * up, through a document listener armed only while it lasts; the CDK then drops it where it
 * started, and the drop that follows is ignored. It owns no announcement: the callbacks do.
 */
export class DragSession<S, T> {
  private readonly source = signal<S | null>(null);
  private readonly onKeydown = (event: KeyboardEvent) => {
    if (event.key === ESCAPE) {
      this.cancel();
    }
  };

  /** What is being dragged; null between drags. */
  readonly dragged = this.source.asReadonly();

  constructor(
    private readonly callbacks: DragCallbacks<S, T>,
    private readonly document: Document,
  ) {}

  start(source: S): void {
    this.source.set(source);
    this.document.addEventListener('keydown', this.onKeydown);
  }

  /** The end of a drag: a move to `target`, or a cancel without one. */
  drop(target: T | null): void {
    const source = this.source();
    if (source === null) {
      return;
    }
    this.reset();
    if (target === null) {
      this.callbacks.onCancel();
    } else {
      this.callbacks.onCommit(source, target);
    }
  }

  cancel(): void {
    if (this.source() !== null) {
      this.reset();
      this.callbacks.onCancel();
    }
  }

  /** Forgets any drag in flight, silently, when its editor goes away. */
  dispose(): void {
    this.reset();
  }

  private reset(): void {
    this.source.set(null);
    this.document.removeEventListener('keydown', this.onKeydown);
  }
}
