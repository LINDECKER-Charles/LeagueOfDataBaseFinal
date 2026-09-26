import type { DialogConfig, DialogRef } from '@angular/cdk/dialog';
import type { DialogOptions } from './dialog-options';

/**
 * CDK configuration of a centred Hextech dialog. The width lives in the `hx-dialog` pane
 * class rather than here, so a phone keeps its margins; the CDK's own 80vw cap is lifted for
 * the same reason. The container takes focus so a screen reader announces the dialog by name
 * before its first control.
 */
export function dialogConfig<D, R, C>(options: DialogOptions<D>): DialogConfig<D, DialogRef<R, C>> {
  return {
    ariaLabelledBy: options.labelledBy,
    ariaModal: true,
    autoFocus: 'dialog',
    backdropClass: 'hx-backdrop',
    data: options.data,
    maxWidth: '100vw',
    panelClass: 'hx-dialog',
    restoreFocus: true,
  };
}
