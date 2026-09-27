import { ChangeDetectionStrategy, Component, Injector, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Button } from '../../../ui/controls/button';

/**
 * The contact entry of the footer, projected by the root component into its `contact` slot:
 * a call to action that opens the contact form in a dialog. The form and the dialog layer
 * are loaded on the first click: every page shows the footer, few send a message.
 */
@Component({
  selector: 'lodb-contact-dialog',
  imports: [Button, TranslocoPipe],
  template: `
    <button lodbButton="gold" type="button" class="w-full justify-center" (click)="open()">
      {{ 'contact.form.cta' | transloco }}
    </button>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContactDialog {
  private readonly injector = inject(Injector);

  protected async open(): Promise<void> {
    const [{ ContactForm }, { DialogService }] = await Promise.all([
      import('../contact-form/contact-form'),
      import('../../../ui/overlays/dialog-service'),
    ]);
    this.injector
      .get(DialogService)
      .open<InstanceType<typeof ContactForm>, undefined, boolean>(ContactForm, {
        labelledBy: ContactForm.HEADING_ID,
        size: 'form',
      });
  }
}
