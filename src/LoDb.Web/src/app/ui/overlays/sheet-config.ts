import type { DialogConfig, DialogRef } from '@angular/cdk/dialog';
import type { PositionStrategy } from '@angular/cdk/overlay';
import { dialogConfig } from './dialog-config';
import type { DialogOptions } from './dialog-options';

/**
 * CDK configuration of a bottom sheet: the dialog's behaviour, pinned to the bottom edge at
 * full width. The CDK has no sheet of its own; a global position strategy makes one.
 */
export function sheetConfig<D, R, C>(
  options: DialogOptions<D>,
  position: PositionStrategy,
): DialogConfig<D, DialogRef<R, C>> {
  return {
    ...dialogConfig<D, R, C>(options),
    panelClass: 'hx-sheet',
    positionStrategy: position,
    width: '100%',
  };
}
