import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, map, timeout } from 'rxjs';
import { API_BASE_URL } from '../../../../core/api/api-base-url';
import { type AdminNotice, AdminNotices } from '../../layout/admin-notices';
import { ADMIN_I18N } from '../admin-i18n';
import type { AdminCall } from './admin-call';
import type { AdminMessage } from './admin-message';
import { ADMIN_TIMEOUT } from './admin-timeout';
import { problemKey } from './problem-key';
import { problemOf } from './problem-of';

/**
 * Runs the actions of the admin (ban, purge, credit…) and tells how they went in the band at
 * the top of the page, as the legacy flash messages did: `done` on success, the reason of the
 * refusal otherwise, from the API's problem code. The panel reloads itself on success; a
 * refusal leaves it as it was. `attempt` says how it went without the band, for a panel that
 * tells it beside the button pressed.
 */
@Injectable({ providedIn: 'root' })
export class AdminCommand {
  private readonly http = inject(HttpClient);
  private readonly rootUrl = inject(API_BASE_URL);
  private readonly notices = inject(AdminNotices);
  private readonly transloco = inject(TranslocoService);
  private readonly wait = inject(ADMIN_TIMEOUT);

  /**
   * Answers the body of the call once it succeeded, null once it failed and said why. `done`
   * may read the body: "3 days consolidated".
   */
  async run<P, T>(
    call: AdminCall<P, T>,
    params: P,
    done: AdminMessage | ((body: T) => AdminMessage),
  ): Promise<T | null> {
    const outcome = await this.attempt(call, params, done);
    this.notices.post(outcome.notice);
    return outcome.body;
  }

  /** Runs the call as `run` does, and answers how it went instead of posting it. */
  async attempt<P, T>(
    call: AdminCall<P, T>,
    params: P,
    done: AdminMessage | ((body: T) => AdminMessage),
  ): Promise<{ readonly body: T | null; readonly notice: AdminNotice }> {
    try {
      const body = await firstValueFrom(
        call(this.http, this.rootUrl, params).pipe(
          timeout({ first: this.wait }),
          map((response) => response.body),
        ),
      );
      const message = typeof done === 'function' ? done(body) : done;
      return { body, notice: { tone: 'notice', text: await this.text(message) } };
    } catch (error) {
      const reason = await this.text({ key: problemKey(problemOf(error)) });
      return { body: null, notice: { tone: 'alert', text: reason } };
    }
  }

  private text(message: AdminMessage): Promise<string> {
    return firstValueFrom(
      this.transloco.selectTranslate<string>(message.key, message.params, ADMIN_I18N.path),
    );
  }
}
