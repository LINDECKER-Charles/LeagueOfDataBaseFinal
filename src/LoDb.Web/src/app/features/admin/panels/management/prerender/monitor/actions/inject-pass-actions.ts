import { type Signal, inject, signal } from '@angular/core';
import { cancelAdminPrerenderPass } from '../../../../../../../core/api/generated/fn/admin-prerender/cancel-admin-prerender-pass';
import { pauseAdminPrerenderPass } from '../../../../../../../core/api/generated/fn/admin-prerender/pause-admin-prerender-pass';
import { resumeAdminPrerenderPass } from '../../../../../../../core/api/generated/fn/admin-prerender/resume-admin-prerender-pass';
import type { AdminNotice } from '../../../../../layout/admin-notices';
import type { AdminCall } from '../../../../../shared/http/admin-call';
import { AdminCommand } from '../../../../../shared/http/admin-command';

/** The moves of the active pass, one at a time. */
export interface PassActions {
  /** Whether a move is in flight. */
  readonly busy: Signal<boolean>;
  /** How the last move went, said in the monitor beside its buttons; null before any. */
  readonly outcome: Signal<AdminNotice | null>;
  pause(id: number): Promise<void>;
  resume(id: number): Promise<void>;
  cancel(id: number): Promise<void>;
}

type PassMove = AdminCall<{ id: number }, void>;

/**
 * The pause, resume and cancel of the monitor, each through `AdminCommand`, which tells how it
 * went: in the monitor, beside the buttons pressed (`outcome`), not in the band at the top of
 * the page, out of sight when the admin acts at the foot of the monitor. `reload` runs after
 * each one, refused or not: a 409 means the pass moved meanwhile (the worker finished it,
 * another admin paused it), and the page must show where it is now rather than what the admin
 * last saw. The launch is the dialog's (launch/call).
 */
export function injectPassActions(reload: () => void): PassActions {
  const command = inject(AdminCommand);
  const busy = signal(false);
  const outcome = signal<AdminNotice | null>(null);
  const move = (call: PassMove, key: string) => async (id: number) => {
    busy.set(true);
    try {
      const done = { key: `prerender.done.${key}`, params: { id } };
      outcome.set((await command.attempt(call, { id }, done)).notice);
    } finally {
      busy.set(false);
      reload();
    }
  };
  return {
    busy: busy.asReadonly(),
    outcome: outcome.asReadonly(),
    pause: move(pauseAdminPrerenderPass, 'pause'),
    resume: move(resumeAdminPrerenderPass, 'resume'),
    cancel: move(cancelAdminPrerenderPass, 'cancel'),
  };
}
