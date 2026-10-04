import type { CdkDrag, CdkDragDrop, CdkDropList } from '@angular/cdk/drag-drop';
import { DOCUMENT, DestroyRef, Injectable, inject } from '@angular/core';
import { DragSession } from './drag-session';
import { insertionIndex } from './insertion-index';
import type { ItemLocation } from './order/item-location';
import { StepEditing } from './step-editing';

type DragSource =
  | { readonly kind: 'step'; readonly index: number }
  | { readonly kind: 'item'; readonly from: ItemLocation };

type DropTarget =
  | { readonly kind: 'step'; readonly insert: number }
  | { readonly kind: 'item'; readonly to: ItemLocation };

/** A drop that ends over its list, as an insertion index; null when released elsewhere. */
function insertOf<T, I>(event: CdkDragDrop<T, T, I>): number | null {
  if (!event.isPointerOverContainer) {
    return null;
  }
  return insertionIndex({
    previousIndex: event.previousIndex,
    currentIndex: event.currentIndex,
    sameList: event.previousContainer === event.container,
  });
}

/**
 * The drags of a step editor, from the CDK's events to the purchase order's moves: steps
 * within their list, items within a step and across steps. The CDK only animates; the order
 * moves on the drop, and a full step refuses the items of another.
 */
@Injectable()
export class StepDrag {
  private readonly editing = inject(StepEditing);
  private readonly session = new DragSession<DragSource, DropTarget>(
    {
      onCommit: (source, target) => this.commit(source, target),
      onCancel: () => this.editing.announceDragCancelled(),
    },
    inject(DOCUMENT),
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => this.session.dispose());
  }

  /** Whether an item may enter the list of a step: its own, or one with room left. */
  readonly canEnter = (drag: CdkDrag<ItemLocation>, drop: CdkDropList<number>): boolean =>
    this.editing.order().canReceiveItem(drop.data, drag.data);

  startStep(index: number): void {
    this.session.start({ kind: 'step', index });
  }

  startItem(from: ItemLocation): void {
    this.session.start({ kind: 'item', from });
  }

  dropStep(event: CdkDragDrop<unknown>): void {
    const insert = insertOf(event);
    this.session.drop(insert === null ? null : { kind: 'step', insert });
  }

  dropItem(event: CdkDragDrop<number, number, ItemLocation>): void {
    const insert = insertOf(event);
    const to = { step: event.container.data, index: insert ?? 0 };
    this.session.drop(insert === null ? null : { kind: 'item', to });
  }

  private commit(source: DragSource, target: DropTarget): void {
    if (source.kind === 'step' && target.kind === 'step') {
      this.editing.dropStep(source.index, target.insert);
    } else if (source.kind === 'item' && target.kind === 'item') {
      this.editing.dropItem(source.from, target.to);
    }
  }
}
