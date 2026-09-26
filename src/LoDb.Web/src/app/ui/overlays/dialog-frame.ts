import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { Icon } from '../media/icon';

/**
 * The frame every dialog body sits in: a heading, a close control, then the projected body.
 * Its heading id is the `labelledBy` the opener passed, so the dialog is named by it.
 * Texts come in as inputs: the design system stays free of any catalogue.
 */
@Component({
  selector: 'lodb-dialog',
  imports: [Icon],
  templateUrl: './dialog-frame.html',
  host: { class: 'hx-dialog-panel' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DialogFrame {
  readonly heading = input.required<string>();
  readonly headingId = input.required<string>();
  readonly closeLabel = input.required<string>();
  private readonly ref = inject(DialogRef, { optional: true });

  protected close(): void {
    this.ref?.close();
  }
}
