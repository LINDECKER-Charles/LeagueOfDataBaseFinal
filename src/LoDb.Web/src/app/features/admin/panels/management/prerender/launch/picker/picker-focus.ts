import { signal } from '@angular/core';
import type { PickerLine } from './picker-rows';

// Keys typed within this delay add up to one search, as in a native list box.
const SEARCH_BUFFER_MS = 600;

/**
 * The roving focus of the list of patches (§ 3.3.5): one line holds the tab stop, so the
 * list is a single stop of Tab, and digits typed in a row move it to the first version they
 * start. The component moves the DOM focus; this only says where.
 */
export class PickerFocus {
  private readonly current = signal<string | null>(null);
  private buffer = '';
  private typedAt = Number.NEGATIVE_INFINITY;

  /** The key of the line holding the tab stop: the current one, else the first shown. */
  keyIn(lines: readonly PickerLine[]): string | null {
    const current = this.current();
    const isShown = lines.some((line) => line.key === current);
    return isShown ? current : (lines[0]?.key ?? null);
  }

  /** Moves the tab stop to the line of `key`. */
  moveTo(key: string): void {
    this.current.set(key);
  }

  /**
   * Adds `character` to the search, a new one past 600 ms, and moves the tab stop to the
   * first patch shown whose version starts with it; its key, or null when none does.
   */
  search(character: string, lines: readonly PickerLine[], now: number): string | null {
    this.buffer = now - this.typedAt > SEARCH_BUFFER_MS ? character : this.buffer + character;
    this.typedAt = now;
    const found = lines.find((line) => line.kind === 'patch' && line.key.startsWith(this.buffer));
    if (!found) {
      return null;
    }
    this.moveTo(found.key);
    return found.key;
  }
}
