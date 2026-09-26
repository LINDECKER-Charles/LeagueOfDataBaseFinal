import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Provisional patch and language switcher, projected by the root component into the
 * `switcher` slot of the header (L3.1). Hidden and empty: the home chantier (L3.9) replaces
 * this file, never the root template.
 */
@Component({
  selector: 'lodb-context-switcher',
  template: '',
  host: { hidden: '' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContextSwitcher {}
