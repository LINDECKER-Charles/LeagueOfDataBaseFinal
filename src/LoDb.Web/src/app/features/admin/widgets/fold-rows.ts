import { type Signal, type WritableSignal, computed, signal } from '@angular/core';

/** A list folded past its limit, as `FoldToggle` opens it. */
export interface Fold<T> {
  /** Whether the folded rows show. */
  readonly open: WritableSignal<boolean>;
  /** How many rows are folded away. */
  readonly folded: Signal<number>;
  /** The rows on screen. */
  readonly shown: Signal<readonly T[]>;
}

/**
 * Folds `rows` past `limit` rows, the legacy collapsible: every row shows while the fold is
 * open, or when `limit` is 0.
 */
export function foldRows<T>(rows: () => readonly T[], limit: () => number): Fold<T> {
  const open = signal(false);
  const folded = computed(() => (limit() > 0 ? Math.max(0, rows().length - limit()) : 0));
  return {
    open,
    folded,
    shown: computed(() => (open() || folded() === 0 ? rows() : rows().slice(0, limit()))),
  };
}
