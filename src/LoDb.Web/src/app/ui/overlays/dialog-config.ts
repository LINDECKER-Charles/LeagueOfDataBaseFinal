import type { DialogConfig, DialogRef } from '@angular/cdk/dialog';
import type { DialogOptions } from './dialog-options';
import type { DialogSize } from './dialog-size';
import { paneClass } from './pane-class';

type ViewerClasses = Pick<DialogConfig, 'backdropClass' | 'panelClass'>;

/** The pane and backdrop of the viewer sizes (styles/primitives/lightbox.css). */
const VIEWER_CLASSES: Partial<Record<DialogSize, ViewerClasses>> = {
  lightbox: { backdropClass: ['hx-backdrop', 'hx-backdrop--lightbox'], panelClass: 'hx-lightbox' },
  compact: {
    backdropClass: ['hx-backdrop', 'hx-backdrop--compact'],
    panelClass: ['hx-lightbox', 'hx-lightbox--compact'],
  },
};

/**
 * CDK configuration of a centred Hextech dialog. The width lives in the `hx-dialog` pane
 * class and its size modifier rather than here, so a phone keeps its margins; the CDK's own
 * 80vw cap is lifted for the same reason. The container takes focus so a screen reader
 * announces the dialog by name before its first control. A viewer size swaps both classes.
 */
export function dialogConfig<D, R, C>(options: DialogOptions<D>): DialogConfig<D, DialogRef<R, C>> {
  return {
    ariaLabelledBy: options.labelledBy,
    ariaModal: true,
    autoFocus: 'dialog',
    backdropClass: 'hx-backdrop',
    data: options.data,
    maxWidth: '100vw',
    panelClass: paneClass('hx-dialog', options.size),
    restoreFocus: true,
    ...(options.size === undefined ? {} : VIEWER_CLASSES[options.size]),
  };
}
