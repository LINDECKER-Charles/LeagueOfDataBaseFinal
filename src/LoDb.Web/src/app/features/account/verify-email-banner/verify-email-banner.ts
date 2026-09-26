import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Provisional banner of an unverified e-mail address, projected by the root component into
 * the `banner` slot (L3.1). Hidden and empty: the account chantier (L4.6) replaces this file,
 * never the root template.
 */
@Component({
  selector: 'lodb-verify-email-banner',
  template: '',
  host: { hidden: '' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VerifyEmailBanner {}
