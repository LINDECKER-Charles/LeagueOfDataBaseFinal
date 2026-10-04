import { Dialog, type DialogRef } from '@angular/cdk/dialog';
import type { ComponentType } from '@angular/cdk/portal';
import { Injectable, Injector, inject } from '@angular/core';
import { createGlobalPositionStrategy } from '@angular/cdk/overlay';
import { dialogConfig } from './dialog-config';
import type { DialogOptions } from './dialog-options';
import { sheetConfig } from './sheet-config';

/**
 * Opens Hextech dialogs and bottom sheets over the CDK Dialog, which brings the focus trap,
 * Escape, the inert page and focus restoration. Like the CDK, each opener hands back the
 * reference that closes the dialog and reports its result. The overlay follows the root
 * Directionality, which PageDirection keeps in step with the locale.
 */
@Injectable({ providedIn: 'root' })
export class DialogService {
  private readonly dialog = inject(Dialog);
  private readonly injector = inject(Injector);

  open<C, D = unknown, R = unknown>(
    component: ComponentType<C>,
    options: DialogOptions<D>,
  ): DialogRef<R, C> {
    return this.dialog.open<R, D, C>(component, dialogConfig<D, R, C>(options));
  }

  openSheet<C, D = unknown, R = unknown>(
    component: ComponentType<C>,
    options: DialogOptions<D>,
  ): DialogRef<R, C> {
    const position = createGlobalPositionStrategy(this.injector).bottom('0').start('0');
    return this.dialog.open<R, D, C>(component, sheetConfig<D, R, C>(options, position));
  }
}
