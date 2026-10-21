import type { DialogRef } from '@angular/cdk/dialog';
import { BreakpointObserver } from '@angular/cdk/layout';
import type { ComponentType } from '@angular/cdk/portal';
import { inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { DialogOptions } from '../../../../../../ui/overlays/dialog-options';
import { DialogService } from '../../../../../../ui/overlays/dialog-service';
import type { ConfirmData } from './confirm-data';
import { ConfirmDialog } from './confirm-dialog';

/** The dialogs of the pre-rendering page. */
export interface AdminDialogs {
  /** A dialog, or a bottom sheet under 640px; the component's host carries `admin-overlay`. */
  open<C, D, R>(component: ComponentType<C>, options: DialogOptions<D>): DialogRef<R, C>;
  /** Resolves true once confirmed, false once kept or closed. */
  confirm(data: ConfirmData): Promise<boolean>;
}

// Below the `sm` breakpoint a dialog rises as a bottom sheet, within thumb reach, as the
// armory does (features/builds/editor/items/armory-opener.ts).
const PHONE = '(max-width: 639.98px)';

/**
 * Opens the dialogs of the pre-rendering page, from the injection context of a component:
 * a centred dialog on a desktop, a bottom sheet on a phone. The CDK mounts them under
 * `<body>`, out of the page: their hosts carry `admin-overlay`, which the admin's sheets
 * style as they style the page (admin-page.css, admin-frame.css).
 */
export function injectAdminDialogs(): AdminDialogs {
  const dialogs = inject(DialogService);
  const breakpoints = inject(BreakpointObserver);
  const open = <C, D, R>(component: ComponentType<C>, options: DialogOptions<D>) =>
    breakpoints.isMatched(PHONE)
      ? dialogs.openSheet<C, D, R>(component, options)
      : dialogs.open<C, D, R>(component, options);
  return {
    open,
    confirm: async (data) => {
      const ref = open<ConfirmDialog, ConfirmData, boolean>(ConfirmDialog, {
        labelledBy: ConfirmDialog.HEADING_ID,
        data,
        size: 'form',
        autoFocus: ConfirmDialog.KEEP,
      });
      return (await firstValueFrom(ref.closed)) === true;
    },
  };
}
