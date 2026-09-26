import type { WritableSignal } from '@angular/core';
import type { PickerLoad } from './picker-load';

/** Reads a list into the signal of a picker, which then says whether it came. */
export async function loadPickerList<T>(
  target: WritableSignal<PickerLoad<T>>,
  list: () => Promise<readonly T[]>,
): Promise<void> {
  target.set({ status: 'loading' });
  try {
    target.set({ status: 'ready', entries: await list() });
  } catch {
    target.set({ status: 'failed' });
  }
}
