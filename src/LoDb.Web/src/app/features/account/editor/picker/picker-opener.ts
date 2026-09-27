import type { DialogRef } from '@angular/cdk/dialog';
import { BreakpointObserver } from '@angular/cdk/layout';
import type { ComponentType } from '@angular/cdk/portal';
import { Injectable, inject } from '@angular/core';
import type { DialogOptions } from '../../../../ui/overlays/dialog-options';
import { DialogService } from '../../../../ui/overlays/dialog-service';

// Below the `md` breakpoint a picker rises as the legacy filter sheet, within thumb reach.
const PHONE = '(max-width: 47.98rem)';
// Typing starts at once, as in the legacy picker: its search holds the focus.
const SEARCH = 'input[type=search]';

/** Opens a picker of the profile editor: a 32rem dialog, a bottom sheet on a phone. */
@Injectable()
export class PickerOpener {
  private readonly dialogs = inject(DialogService);
  private readonly breakpoints = inject(BreakpointObserver);

  open<C, D, R>(component: ComponentType<C>, labelledBy: string, data: D): DialogRef<R, C> {
    const options: DialogOptions<D> = { labelledBy, data, size: 'picker', autoFocus: SEARCH };
    return this.breakpoints.isMatched(PHONE)
      ? this.dialogs.openSheet<C, D, R>(component, options)
      : this.dialogs.open<C, D, R>(component, options);
  }
}
