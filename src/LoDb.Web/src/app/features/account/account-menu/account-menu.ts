import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Provisional account menu, projected by the root component into the `account` slot of the
 * header (L3.1). Hidden and empty: the account chantier (L4.6) replaces this file, never the
 * root template.
 */
@Component({
  selector: 'lodb-account-menu',
  template: '',
  host: { hidden: '' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountMenu {}
