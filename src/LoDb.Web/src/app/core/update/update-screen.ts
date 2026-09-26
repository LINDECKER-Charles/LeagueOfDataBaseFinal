import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * Blocking update screen of the apps (L9.0), drawn over the whole application when the API
 * refuses their version. The root component places it after the shell since L3.1; it stays
 * hidden and empty until L9.0 replaces it.
 */
@Component({
  selector: 'lodb-update-screen',
  template: '',
  host: { hidden: '' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UpdateScreen {}
