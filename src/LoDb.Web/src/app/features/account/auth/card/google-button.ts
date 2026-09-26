import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { RETURN_URL_PARAM } from '../../../../core/auth/guards/return-url-param';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { Button } from '../../../../ui/controls/button';

/**
 * "Continue with Google" under an "or" hairline. It leaves the application, a full-page
 * navigation on the web, then comes back to the page the sign-in was asked to return to.
 */
@Component({
  selector: 'lodb-google-button',
  imports: [Button, TranslocoPipe],
  template: `<div class="auth-divider" aria-hidden="true">
      <span>{{ 'auth.or' | transloco }}</span>
    </div>
    <button
      type="button"
      lodbButton="ghost"
      class="w-full"
      [disabled]="leaving()"
      (click)="start()"
    >
      <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor" aria-hidden="true">
        <path
          d="M12.24 10.285V14.4h6.806c-.275 1.765-2.056 5.174-6.806 5.174-4.095 0-7.439-3.389
            -7.439-7.574s3.345-7.574 7.439-7.574c2.33 0 3.891.989 4.785 1.849l3.254-3.138
            C18.189 1.186 15.479 0 12.24 0c-6.635 0-12 5.365-12 12s5.365 12 12 12
            c6.926 0 11.52-4.869 11.52-11.726 0-.788-.085-1.39-.189-1.989H12.24z"
        />
      </svg>
      {{ 'auth.google.cta' | transloco }}
    </button>`,
  styleUrl: './auth-form.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GoogleButton {
  /** "Keep me signed in", as ticked on the login form. */
  readonly rememberMe = input(false);

  private readonly session = inject(AuthSession);
  private readonly route = inject(ActivatedRoute);
  private readonly page = inject(PageDirection);
  protected readonly leaving = signal(false);

  protected start(): void {
    const locale = this.page.locale();
    this.leaving.set(true);
    this.session
      .startGoogleSignIn({
        returnUrl: this.route.snapshot.queryParamMap.get(RETURN_URL_PARAM),
        fallbackUrl: localePath(locale, 'account/profile'),
        locale,
        rememberMe: this.rememberMe(),
      })
      .finally(() => this.leaving.set(false));
  }
}
