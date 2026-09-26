import { Injectable, inject, signal } from '@angular/core';
import { EDITOR_ENTRY } from '../form/editor-entry-token';
import { EditorAnnouncer } from './editor-announcer';
import type { ItemLocation } from './order/item-location';
import { PurchaseOrder } from './order/purchase-order';
import type { StepPatch } from './order/step-patch';

// The announcements of the moves, `build.editor.dnd.<move>`.
const MOVES = 'build.editor.dnd.';

/**
 * The purchase order of an editor, and every change the author makes to it: typing, the
 * buttons and the drags all go through the order's own rules. A move is announced to screen
 * readers, a move that changes nothing is not.
 */
@Injectable()
export class StepEditing {
  private readonly announcer = inject(EditorAnnouncer);
  private readonly current = signal(PurchaseOrder.of(inject(EDITOR_ENTRY).draft.structure.steps));

  readonly order = this.current.asReadonly();

  appendStep(label = ''): void {
    this.current.update((order) => order.withStep(label));
  }

  deleteStep(index: number): void {
    this.current.update((order) => order.withoutStep(index));
  }

  editStep(index: number, patch: StepPatch): void {
    this.current.update((order) => order.patchingStep(index, patch));
  }

  shiftStep(index: number, delta: number): void {
    const next = this.order().movingStep(index, delta);
    this.commit(next, 'moved_step', { position: index + delta + 1 });
  }

  dropStep(from: number, insertIndex: number): void {
    const order = this.order();
    const resting = PurchaseOrder.restingIndex(from, insertIndex, order.steps.length);
    this.commit(order.movingStepTo(from, insertIndex), 'moved_step', { position: resting + 1 });
  }

  appendItem(step: number, itemId: string): void {
    this.commit(this.order().withItem(step, itemId), 'added', { step: step + 1 });
  }

  deleteItem(at: ItemLocation): void {
    this.current.update((order) => order.withoutItem(at));
  }

  shiftItem(at: ItemLocation, delta: number): void {
    const next = this.order().movingItem(at, delta);
    this.commit(next, 'moved_item', { position: at.index + delta + 1 });
  }

  /** A drop of an item: a reorder within its step, a transfer to another. */
  dropItem(from: ItemLocation, to: ItemLocation): void {
    const order = this.order();
    if (from.step !== to.step) {
      this.commit(order.transferringItem(from, to), 'transferred', { step: to.step + 1 });
      return;
    }
    const length = order.steps[from.step]?.items.length ?? 0;
    const resting = PurchaseOrder.restingIndex(from.index, to.index, length);
    this.commit(order.movingItemTo(from, to.index), 'moved_item', { position: resting + 1 });
  }

  announceDragCancelled(): void {
    this.announcer.say(`${MOVES}cancelled`);
  }

  private commit(next: PurchaseOrder, move: string, params: Record<string, number>): void {
    if (next !== this.order()) {
      this.current.set(next);
      this.announcer.say(`${MOVES}${move}`, params);
    }
  }
}
