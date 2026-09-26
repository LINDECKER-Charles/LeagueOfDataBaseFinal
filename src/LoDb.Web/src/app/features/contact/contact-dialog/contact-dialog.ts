import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Provisional contact entry and dialog, projected by the root component into the `contact`
 * slot of the footer (L3.1). Hidden and empty: the contact chantier (L7.5) replaces this
 * file, never the root template.
 */
@Component({
  selector: 'lodb-contact-dialog',
  template: '',
  host: { hidden: '' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContactDialog {}
