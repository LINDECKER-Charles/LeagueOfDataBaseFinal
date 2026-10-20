import { type Signal, inject, signal } from '@angular/core';
import { cancelAdminPrerenderPass } from '../../../../../core/api/generated/fn/admin-prerender/cancel-admin-prerender-pass';
import { pauseAdminPrerenderPass } from '../../../../../core/api/generated/fn/admin-prerender/pause-admin-prerender-pass';
import { resumeAdminPrerenderPass } from '../../../../../core/api/generated/fn/admin-prerender/resume-admin-prerender-pass';
import { startAdminPrerenderPass } from '../../../../../core/api/generated/fn/admin-prerender/start-admin-prerender-pass';
import type { PassRequest } from '../../../../../core/api/generated/models/pass-request';
import type { AdminCall } from '../../../shared/http/admin-call';
import { AdminCommand } from '../../../shared/http/admin-command';
import type { AdminMessage } from '../../../shared/http/admin-message';

/** The actions on the passes of the pre-rendering, one at a time. */
export interface PassActions {
  /** Whether an action is in flight. */
  readonly busy: Signal<boolean>;
  start(request: PassRequest): Promise<void>;
  pause(id: number): Promise<void>;
  resume(id: number): Promise<void>;
  cancel(id: number): Promise<void>;
}

type PassMove = AdminCall<{ id: number }, void>;

/**
 * The actions of the pre-rendering panel, each through `AdminCommand`, which tells how it
 * went. The panel reads itself again after each one, refused or not: a 409 means the pass
 * moved meanwhile (the worker finished it, another admin paused it), and the panel must show
 * where it is now rather than what the admin last saw.
 */
export function injectPassActions(reload: () => void): PassActions {
  const command = inject(AdminCommand);
  const busy = signal(false);
  const run = async <P, T>(
    call: AdminCall<P, T>,
    params: P,
    done: AdminMessage | ((body: T) => AdminMessage),
  ): Promise<void> => {
    busy.set(true);
    try {
      await command.run(call, params, done);
    } finally {
      busy.set(false);
      reload();
    }
  };
  const move = (call: PassMove, key: string) => (id: number) =>
    run(call, { id }, { key: `prerender.done.${key}`, params: { id } });
  return {
    busy: busy.asReadonly(),
    start: (request) =>
      run(startAdminPrerenderPass, { body: request }, (started) => ({
        key: 'prerender.done.start',
        params: { id: started.id, epoch: started.epoch, units: started.units },
      })),
    pause: move(pauseAdminPrerenderPass, 'pause'),
    resume: move(resumeAdminPrerenderPass, 'resume'),
    cancel: move(cancelAdminPrerenderPass, 'cancel'),
  };
}
