import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Shell } from './core/layout/shell/shell';
import { UpdateBanner } from './core/update/update-banner';
import { UpdateScreen } from './core/update/update-screen';
import { AccountMenu } from './features/account/account-menu/account-menu';
import { VerifyEmailBanner } from './features/account/verify-email-banner/verify-email-banner';
import { ContactDialog } from './features/contact/contact-dialog/contact-dialog';
import { ContextSwitcher } from './features/context-switcher/context-switcher';

/**
 * Root of the application: the shell with a component in each of its slots (plan, section
 * 5.2), the page in its outlet, and the blocking update screen of the apps over everything.
 * Each slot component is its chantier's to replace, in its own file: this template stays.
 */
@Component({
  selector: 'lodb-root',
  imports: [
    AccountMenu,
    ContactDialog,
    ContextSwitcher,
    RouterOutlet,
    Shell,
    UpdateBanner,
    UpdateScreen,
    VerifyEmailBanner,
  ],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
