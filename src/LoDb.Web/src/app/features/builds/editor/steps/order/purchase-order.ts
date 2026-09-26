import type { BuildStep } from '../../../../../core/api/generated/models/build-step';
import type { StepBody } from '../../../../../core/api/generated/models/step-body';
import type { ItemLocation } from './item-location';
import type { OrderStep } from './order-step';
import type { StepPatch } from './step-patch';
import { STEP_LIMITS } from './step-limits';

/** The gold of an item, null when no catalogue knows it (a ghost). */
type GoldOf = (itemId: string) => number | null;

function blankStep(label = ''): OrderStep {
  return { label, note: null, items: [] };
}

function clampInsert(index: number, length: number): number {
  return Math.max(0, Math.min(length, index));
}

// Removing the dragged entry first shifts the insertion points past it down by one.
function restingIndexOf(from: number, insertIndex: number, length: number): number {
  const target = clampInsert(insertIndex, length);
  return target > from ? target - 1 : target;
}

function inserted<T>(entries: readonly T[], at: number, entry: T): readonly T[] {
  return [...entries.slice(0, at), entry, ...entries.slice(at)];
}

function relocated<T>(entries: readonly T[], from: number, to: number): readonly T[] {
  const rest = entries.filter((_, index) => index !== from);
  return inserted(rest, to, entries[from]);
}

// Swaps an entry with a neighbour; the same list when it cannot move.
function moveEntry<T>(entries: readonly T[], index: number, delta: number): readonly T[] {
  const target = index + delta;
  const inside = (at: number) => at >= 0 && at < entries.length;
  return delta === 0 || !inside(index) || !inside(target)
    ? entries
    : relocated(entries, index, target);
}

// Moves an entry to an insertion index (drop semantics); the same list when nothing moves.
function moveEntryToIndex<T>(entries: readonly T[], from: number, insertIndex: number) {
  if (from < 0 || from >= entries.length) {
    return entries;
  }
  const resting = restingIndexOf(from, insertIndex, entries.length);
  return resting === from ? entries : relocated(entries, from, resting);
}

/**
 * The purchase order of a build: its steps, each a label, an optional note and items. Every
 * change answers a new order, and the same one when the change is out of bounds or changes
 * nothing, so that a caller tells a real move from a refused one by identity. Positions
 * given to the `...To` moves are insertion indexes, from 0 to the length, as a drop reads.
 */
export class PurchaseOrder {
  private constructor(readonly steps: readonly OrderStep[]) {}

  /** The order of a stored build; a first blank step when it has none. */
  static of(steps: readonly BuildStep[]): PurchaseOrder {
    const copied = steps.map((step) => ({ ...step, note: step.note ?? null, items: step.items }));
    return new PurchaseOrder(copied.length > 0 ? copied : [blankStep()]);
  }

  /** Where an entry dragged from `from` lands for a drop at `insertIndex`. */
  static restingIndex(from: number, insertIndex: number, length: number): number {
    return restingIndexOf(from, insertIndex, length);
  }

  get totalItems(): number {
    return this.steps.reduce((sum, step) => sum + step.items.length, 0);
  }

  canAddStep(): boolean {
    return this.steps.length < STEP_LIMITS.maxSteps;
  }

  canRemoveStep(): boolean {
    return this.steps.length > STEP_LIMITS.minSteps;
  }

  withStep(label = ''): PurchaseOrder {
    return this.canAddStep() ? new PurchaseOrder([...this.steps, blankStep(label)]) : this;
  }

  withoutStep(index: number): PurchaseOrder {
    return this.canRemoveStep() && this.hasStep(index)
      ? new PurchaseOrder(this.steps.filter((_, at) => at !== index))
      : this;
  }

  movingStep(index: number, delta: number): PurchaseOrder {
    return this.withSteps(moveEntry(this.steps, index, delta));
  }

  movingStepTo(from: number, insertIndex: number): PurchaseOrder {
    return this.withSteps(moveEntryToIndex(this.steps, from, insertIndex));
  }

  patchingStep(index: number, patch: StepPatch): PurchaseOrder {
    return this.hasStep(index) ? this.replacing(index, { ...this.steps[index], ...patch }) : this;
  }

  /** Whether a step takes one more item: its own cap and the build's. */
  canAddItem(step: number): boolean {
    return (
      this.hasStep(step) &&
      this.steps[step].items.length < STEP_LIMITS.maxItemsPerStep &&
      this.totalItems < STEP_LIMITS.maxTotalItems
    );
  }

  /**
   * Whether a step takes an item moved from `from`: a move within a step never changes a
   * count, a move across steps leaves the build's total alone and only meets the target's cap.
   */
  canReceiveItem(step: number, from: ItemLocation): boolean {
    const count = this.steps[step]?.items.length ?? STEP_LIMITS.maxItemsPerStep;
    return from.step === step || count < STEP_LIMITS.maxItemsPerStep;
  }

  withItem(step: number, itemId: string): PurchaseOrder {
    return this.insertingItem({ step, index: this.steps[step]?.items.length ?? 0 }, itemId);
  }

  /** Inserts at a place, clamped to the step; the caps of {@link canAddItem} apply. */
  insertingItem(at: ItemLocation, itemId: string): PurchaseOrder {
    if (!this.canAddItem(at.step)) {
      return this;
    }
    const items = this.steps[at.step].items;
    return this.replacingItems(
      at.step,
      inserted(items, clampInsert(at.index, items.length), itemId),
    );
  }

  withoutItem(at: ItemLocation): PurchaseOrder {
    const items = this.steps[at.step]?.items ?? [];
    return at.index >= 0 && at.index < items.length
      ? this.replacingItems(
          at.step,
          items.filter((_, index) => index !== at.index),
        )
      : this;
  }

  movingItem(from: ItemLocation, delta: number): PurchaseOrder {
    const items = this.steps[from.step]?.items;
    return items ? this.replacingItems(from.step, moveEntry(items, from.index, delta)) : this;
  }

  movingItemTo(from: ItemLocation, insertIndex: number): PurchaseOrder {
    const items = this.steps[from.step]?.items;
    return items
      ? this.replacingItems(from.step, moveEntryToIndex(items, from.index, insertIndex))
      : this;
  }

  /**
   * Moves an item to another step, at an insertion index there. The build's total stays the
   * same, so only the target's cap applies; within one step, it is a plain reorder. The
   * source may end empty: the API asks for an item per step when the build is saved.
   */
  transferringItem(from: ItemLocation, to: ItemLocation): PurchaseOrder {
    if (from.step === to.step) {
      return this.movingItemTo(from, to.index);
    }
    const itemId = this.steps[from.step]?.items[from.index];
    const target = this.steps[to.step]?.items;
    if (itemId === undefined || !target || target.length >= STEP_LIMITS.maxItemsPerStep) {
      return this;
    }
    const at = clampInsert(to.index, target.length);
    return this.withoutItem(from).replacingItems(to.step, inserted(target, at, itemId));
  }

  /** The gold of a step; a ghost counts for nothing. */
  stepGold(index: number, goldOf: GoldOf): number {
    const items = this.steps[index]?.items ?? [];
    return items.reduce((sum, itemId) => sum + (goldOf(itemId) ?? 0), 0);
  }

  /** The gold of the whole build. */
  gold(goldOf: GoldOf): number {
    return this.steps.reduce((sum, _, index) => sum + this.stepGold(index, goldOf), 0);
  }

  /** The steps as the API takes them. */
  toSteps(): StepBody[] {
    return this.steps.map((step) => ({
      label: step.label,
      note: step.note,
      items: [...step.items],
    }));
  }

  private hasStep(index: number): boolean {
    return index >= 0 && index < this.steps.length;
  }

  private withSteps(steps: readonly OrderStep[]): PurchaseOrder {
    return steps === this.steps ? this : new PurchaseOrder(steps);
  }

  private replacing(index: number, step: OrderStep): PurchaseOrder {
    return new PurchaseOrder(this.steps.map((known, at) => (at === index ? step : known)));
  }

  private replacingItems(index: number, items: readonly string[]): PurchaseOrder {
    const step = this.steps[index];
    return !step || items === step.items ? this : this.replacing(index, { ...step, items });
  }
}
