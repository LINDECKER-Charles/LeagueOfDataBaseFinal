import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { Icon } from '../media/icon';

/**
 * The frame every dialog body sits in: a heading, a close control, then the projected body.
 * Its heading id is the `labelledBy` the opener passed, so the dialog is named by it.
 * Texts come in as inputs: the design system stays free of any catalogue.
 *
 * The heading is a small-caps eyebrow on its own (the theme picker). With an `eyebrow`, the
 * frame is a form's: the eyebrow above a display title, and a larger close (the contact form).
 * An element marked `lodbDialogLead` leads the head, before the heading: a way back, whose
 * square matches the close's so that the heading sits centred between them.
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
  /** Small caps above the heading, which then reads as a display title. */
  readonly eyebrow = input<string | null>(null);
  private readonly ref = inject(DialogRef, { optional: true });

  protected close(): void {
    this.ref?.close();
  }
}
