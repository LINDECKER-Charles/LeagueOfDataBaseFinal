import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Button } from '../../../../../../ui/controls/button';
import { DialogFrame } from '../../../../../../ui/overlays/dialog-frame';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import type { ConfirmData } from './confirm-data';

/**
 * Asks before an action of consequence (§ 4.7): its title, what will happen, then the safe
 * choice, focused on opening so that Enter never confirms by mistake, and the action itself.
 * It closes on true once confirmed; Escape, the close and the safe choice close on false.
 */
@Component({
  selector: 'lodb-confirm-dialog',
  imports: [Button, DialogFrame, AdminTextPipe],
  template: `
    <lodb-dialog
      [heading]="data.title"
      [headingId]="headingId"
      [closeLabel]="'admin.prerender.common.close' | adminText"
    >
      <ul class="flex list-disc flex-col gap-2 ps-5 text-[0.9rem] text-text-muted">
        @for (line of data.lines; track $index) {
          <li>{{ line }}</li>
        }
      </ul>
      <div class="mt-6 flex flex-wrap justify-end gap-3">
        <button lodbButton="ghost" type="button" data-confirm-keep (click)="ref.close(false)">
          {{ data.keepLabel }}
        </button>
        <button [lodbButton]="data.tone" type="button" data-confirm (click)="ref.close(true)">
          {{ data.confirmLabel }}
        </button>
      </div>
    </lodb-dialog>
  `,
  host: { class: 'admin-overlay block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialog {
  /** Id of the dialog's heading, which names it: the opener's `labelledBy`. */
  static readonly HEADING_ID = 'prerender-confirm-title';
  /** The control focused on opening: the safe choice. */
  static readonly KEEP = '[data-confirm-keep]';

  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);
  protected readonly data = inject<ConfirmData>(DIALOG_DATA);
  protected readonly headingId = ConfirmDialog.HEADING_ID;
}
