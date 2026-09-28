import { inject, type Signal, signal } from '@angular/core';
import { AdminCommand } from './admin-command';
import type { RowAction } from './row-action';

/** The actions on the rows of a list, one at a time. */
export interface RowActions {
  /** The id of the row whose action is in flight, null when none is. */
  readonly pending: Signal<number | null>;
  /** Runs `action` on row `id`, then reloads the list once it succeeded. */
  run<P, T>(id: number, action: RowAction<P, T>): Promise<T | null>;
}

/**
 * The actions of a list of the admin (ban, unpublish, revoke…): each runs through
 * `AdminCommand`, which tells how it went, and the list reloads after a success so that it
 * shows what the API now holds.
 */
export function injectRowActions(reload: () => void): RowActions {
  const command = inject(AdminCommand);
  const pending = signal<number | null>(null);
  return {
    pending: pending.asReadonly(),
    run: async (id, action) => {
      pending.set(id);
      try {
        const body = await command.run(action.call, action.params, action.done);
        if (body !== null) {
          reload();
        }
        return body;
      } finally {
        pending.set(null);
      }
    },
  };
}
