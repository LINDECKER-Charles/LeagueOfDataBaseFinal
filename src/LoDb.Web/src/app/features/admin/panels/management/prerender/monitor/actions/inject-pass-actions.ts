import { type Signal, inject, signal } from '@angular/core';
import { cancelAdminPrerenderPass } from '../../../../../../../core/api/generated/fn/admin-prerender/cancel-admin-prerender-pass';
import { pauseAdminPrerenderPass } from '../../../../../../../core/api/generated/fn/admin-prerender/pause-admin-prerender-pass';
import { resumeAdminPrerenderPass } from '../../../../../../../core/api/generated/fn/admin-prerender/resume-admin-prerender-pass';
import type { AdminCall } from '../../../../../shared/http/admin-call';
import { AdminCommand } from '../../../../../shared/http/admin-command';

/** The moves of the active pass, one at a time. */
export interface PassActions {
  /** Whether a move is in flight. */
  readonly busy: Signal<boolean>;
  pause(id: number): Promise<void>;
  resume(id: number): Promise<void>;
  cancel(id: number): Promise<void>;
}

type PassMove = AdminCall<{ id: number }, void>;

/**
 * The pause, resume and cancel of the monitor, each through `AdminCommand`, which tells how it
 * went. `reload` runs after each one, refused or not: a 409 means the pass moved meanwhile
 * (the worker finished it, another admin paused it), and the page must show where it is now
 * rather than what the admin last saw. The launch is the dialog's (launch/call).
 */
export function injectPassActions(reload: () => void): PassActions {
  const command = inject(AdminCommand);
  const busy = signal(false);
  const move = (call: PassMove, key: string) => async (id: number) => {
    busy.set(true);
    try {
      await command.run(call, { id }, { key: `prerender.done.${key}`, params: { id } });
    } finally {
      busy.set(false);
      reload();
    }
  };
  return {
    busy: busy.asReadonly(),
    pause: move(pauseAdminPrerenderPass, 'pause'),
    resume: move(resumeAdminPrerenderPass, 'resume'),
    cancel: move(cancelAdminPrerenderPass, 'cancel'),
  };
}
