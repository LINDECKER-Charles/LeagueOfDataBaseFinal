import { ChangeDetectionStrategy, Component } from '@angular/core';
import { provideTranslocoScope } from '@jsverse/transloco';
import { injectRouteData } from '../../core/routing/inject-route-data';
import type { AccountView } from './account-view';
import { LoginView } from './auth/login/login-view';
import { ForgotPasswordView } from './auth/recovery/forgot-password-view';
import { ResetPasswordView } from './auth/recovery/reset-password-view';
import { RegisterView } from './auth/register/register-view';
import { VerifyEmailView } from './auth/verify/verify-email-view';
import { ProfileEditor } from './editor/profile-editor';
import { ProfilePreview } from './preview/profile-preview';
import { ACCOUNT_SCOPE } from './shared/account-scope';
import { applyPrivateHead } from './shared/apply-private-head';

/**
 * The page of every account route, rendered in the browser only: it writes the private head
 * of its title and shows the view its route names, each loaded on its own, so that the
 * sign-in never downloads the profile editor. The account texts are its scope's.
 */
@Component({
  selector: 'lodb-account-page',
  imports: [
    ForgotPasswordView,
    LoginView,
    ProfileEditor,
    ProfilePreview,
    RegisterView,
    ResetPasswordView,
    VerifyEmailView,
  ],
  providers: [provideTranslocoScope(ACCOUNT_SCOPE)],
  templateUrl: './account-page.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountPage {
  protected readonly view = injectRouteData<AccountView>('view');

  constructor() {
    applyPrivateHead(injectRouteData<string>('heading'));
  }
}
