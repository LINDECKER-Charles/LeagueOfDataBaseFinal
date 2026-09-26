import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * "Update ready, restart" banner of the apps (L9.0), fed by `PlatformService.updateState`.
 * The root component projects it into the `banner` slot since L3.1; it stays hidden and
 * empty until L9.0 replaces it.
 */
@Component({
  selector: 'lodb-update-banner',
  template: '',
  host: { hidden: '' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UpdateBanner {}
