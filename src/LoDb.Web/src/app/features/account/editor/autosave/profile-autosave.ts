import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import type { AutosaveStatus } from './autosave-status';
import type { ProfileSave } from './profile-save';

// Long enough to gather a burst of picks into one request, short enough to feel immediate.
const DEBOUNCE_MS = 500;
// How long a clean save says so before the status clears.
const SAVED_LINGER_MS = 2500;

/**
 * The automatic save of the profile editor. Each change schedules the save of its part
 * (favorites, visibility) under a key: a pause of half a second gathers them, a later save
 * of a key replaces the earlier one, and the saves run one after the other. A clean save
 * fades back to idle, a warning or a failure stays; the failed saves wait for `retry`.
 * Leaving the editor sends what is still waiting.
 */
@Injectable()
export class ProfileAutosave {
  private readonly pending = new Map<string, ProfileSave>();
  private readonly failed = new Map<string, ProfileSave>();
  private readonly current = signal<AutosaveStatus>('idle');
  private debounce: ReturnType<typeof setTimeout> | undefined;
  private linger: ReturnType<typeof setTimeout> | undefined;
  private running: Promise<void> = Promise.resolve();

  readonly status = this.current.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.linger);
      void this.flush();
    });
  }

  /** Saves a part of the profile after a pause; a later save of the same key replaces it. */
  schedule(key: string, save: ProfileSave): void {
    this.pending.set(key, save);
    this.failed.delete(key);
    clearTimeout(this.debounce);
    clearTimeout(this.linger);
    this.debounce = setTimeout(() => void this.flush(), DEBOUNCE_MS);
  }

  /** Sends the saves that failed again, at once, with whatever else waits. */
  retry(): Promise<void> {
    for (const [key, save] of this.failed) {
      if (!this.pending.has(key)) {
        this.pending.set(key, save);
      }
    }
    this.failed.clear();
    return this.flush();
  }

  /** Sends what waits now, after the saves already on their way. */
  flush(): Promise<void> {
    clearTimeout(this.debounce);
    if (this.pending.size === 0) {
      return this.running;
    }
    const batch = [...this.pending];
    this.pending.clear();
    this.running = this.running.then(() => this.run(batch));
    return this.running;
  }

  private async run(batch: readonly (readonly [string, ProfileSave])[]): Promise<void> {
    this.current.set('saving');
    const outcomes: AutosaveStatus[] = [];
    for (const [key, save] of batch) {
      outcomes.push(await this.attempt(key, save));
    }
    this.settle(outcomes);
  }

  private async attempt(key: string, save: ProfileSave): Promise<AutosaveStatus> {
    try {
      return await save();
    } catch {
      this.failed.set(key, save);
      return 'error';
    }
  }

  private settle(outcomes: readonly AutosaveStatus[]): void {
    const status = outcomes.includes('error')
      ? 'error'
      : outcomes.includes('warned')
        ? 'warned'
        : 'saved';
    this.current.set(status);
    if (status === 'saved') {
      this.linger = setTimeout(() => this.current.set('idle'), SAVED_LINGER_MS);
    }
  }
}
