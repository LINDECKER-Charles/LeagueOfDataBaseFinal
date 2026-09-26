import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Button } from '../controls/button';
import { Field } from '../controls/field';
import { DialogFrame } from '../overlays/dialog-frame';

/** Sample body for the gallery's dialog and bottom sheet: a short form and its actions. */
@Component({
  selector: 'lodb-gallery-dialog',
  imports: [Button, DialogFrame, Field],
  template: `
    <lodb-dialog heading="Hextech dialog" [headingId]="titleId" closeLabel="Close">
      <p class="mb-4 text-sm text-text-muted">
        Focus is trapped, Escape closes, the page behind is inert and focus returns to the opener.
      </p>
      <label class="mb-4 flex flex-col gap-1.5 text-sm">
        <span class="text-text-muted">Summoner name</span>
        <input lodbField autocomplete="off" />
      </label>
      <div class="flex flex-wrap justify-end gap-3">
        <button type="button" lodbButton="ghost" (click)="ref.close()">Cancel</button>
        <button type="button" lodbButton="gold" (click)="ref.close()">Confirm</button>
      </div>
    </lodb-dialog>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GalleryDialog {
  static readonly titleId = 'gallery-dialog-title';

  protected readonly titleId = GalleryDialog.titleId;
  protected readonly ref = inject(DialogRef);
}
