import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom, timeout } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import { confirmAdminMfa } from '../../../core/api/generated/fn/admin-mfa/confirm-admin-mfa';
import { startAdminMfaEnrollment } from '../../../core/api/generated/fn/admin-mfa/start-admin-mfa-enrollment';
import { AuthSession } from '../../../core/auth/session/auth-session';
import { Button } from '../../../ui/controls/button';
import { Field } from '../../../ui/controls/field';
import { Frame } from '../../../ui/surfaces/frame';
import { ADMIN_PATHS } from '../shared/admin-paths';
import { ADMIN_TIMEOUT } from '../shared/http/admin-timeout';
import { formText } from '../shared/form-text';
import { problemOf } from '../shared/http/problem-of';
import { injectPanel } from '../state/inject-panel';
import { PanelState } from '../state/panel-state';
import { PageHead } from '../widgets/page-head';
import { enrollErrorKey } from './enroll-error-key';
import { QrImage } from './qr/qr-image';

// The shared key reads in groups of four, as the authenticator apps print it.
const KEY_GROUPS = /.{1,4}/g;

/**
 * `/admin/enroll`: an administrator without an authenticator enrols one before anything
 * else, as the API's `Admin` policy requires a session opened with a second factor. The
 * page draws a new key (a QR code and its text), checks a first code of it, then hands out
 * the recovery codes, shown only this once; the session is then open for the admin.
 */
@Component({
  selector: 'lodb-admin-enroll-page',
  imports: [Button, Field, Frame, PageHead, PanelState, QrImage, RouterLink, TranslocoPipe],
  templateUrl: './admin-enroll-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminEnrollPage {
  private readonly http = inject(HttpClient);
  private readonly rootUrl = inject(API_BASE_URL);
  private readonly wait = inject(ADMIN_TIMEOUT);
  private readonly session = inject(AuthSession);

  protected readonly setup = injectPanel(startAdminMfaEnrollment, () => ({}));
  protected readonly sharedKey = computed(
    () => this.setup.value()?.sharedKey.match(KEY_GROUPS)?.join(' ') ?? '',
  );
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly recoveryCodes = signal<readonly string[] | null>(null);
  protected readonly home = ADMIN_PATHS.home;

  protected confirm(event: Event): void {
    void this.send(formText(event, 'code'));
  }

  private async send(code: string): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const response = await firstValueFrom(
        confirmAdminMfa(this.http, this.rootUrl, { body: { code } }).pipe(
          timeout({ first: this.wait }),
        ),
      );
      this.session.apply(response.body.session);
      this.recoveryCodes.set(response.body.recoveryCodes);
    } catch (error) {
      this.error.set(enrollErrorKey(problemOf(error)));
    } finally {
      this.busy.set(false);
    }
  }
}
