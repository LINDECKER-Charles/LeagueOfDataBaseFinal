import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../../../core/layout/toast/toast-service';
import type { ToastKind } from '../../../core/layout/toast/toast-kind';
import { Button } from '../../controls/button';
import { DialogService } from '../../overlays/dialog-service';
import { GalleryDialog } from '../gallery-dialog';

/** The dialog, the bottom sheet and the four toast kinds, opened on demand. */
@Component({
  selector: 'lodb-gallery-overlays',
  imports: [Button],
  templateUrl: './overlays-section.html',
  host: { class: 'block scroll-mt-20' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OverlaysSection {
  protected readonly kinds: readonly ToastKind[] = ['info', 'success', 'warning', 'error'];
  private readonly dialogs = inject(DialogService);
  private readonly toasts = inject(ToastService);

  protected openDialog(): void {
    this.dialogs.open(GalleryDialog, { labelledBy: GalleryDialog.titleId });
  }

  protected openSheet(): void {
    this.dialogs.openSheet(GalleryDialog, { labelledBy: GalleryDialog.titleId });
  }

  protected toast(kind: ToastKind): void {
    this.toasts.show(kind, `A ${kind} toast.\nIts full text wraps; it leaves after five seconds.`);
  }
}
