import { DialogRef } from '@angular/cdk/dialog';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import { sendContactMessage } from '../../../core/api/generated/fn/contact/send-contact-message';
import type { ContactRequest } from '../../../core/api/generated/models/contact-request';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import { ToastService } from '../../../core/layout/toast/toast-service';
import { Button } from '../../../ui/controls/button';
import { Field } from '../../../ui/controls/field';
import { DialogFrame } from '../../../ui/overlays/dialog-frame';
import { CONTACT_CATEGORIES } from './contact-categories';
import { contactFieldErrors, type ContactFieldErrors } from './contact-field-errors';
import { contactProblem, type ContactProblem } from './contact-problem';

const TOO_MANY_REQUESTS = 429;
const FIELDS = ['category', 'name', 'email', 'subject', 'message', 'website'] as const;

/**
 * The contact form of the footer, in a dialog: a reason, a name, an e-mail, a subject and
 * the message, in the locale of the page. A sent message closes the dialog on a toast; a
 * refused one keeps what was typed, its fields marked, or tells to wait when the limit ran
 * out. The `website` field is a honeypot, off screen and out of the tab order.
 */
@Component({
  selector: 'lodb-contact-form',
  imports: [Button, DialogFrame, Field, TranslocoPipe],
  templateUrl: './contact-form.html',
  styleUrl: './contact-form.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContactForm {
  /** Id of the form's heading, which names the dialog: the opener's `labelledBy`. */
  static readonly HEADING_ID = 'lodb-contact-form-heading';

  private readonly http = inject(HttpClient);
  private readonly apiOrigin = inject(API_BASE_URL);
  private readonly page = inject(PageDirection);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);

  protected readonly headingId = ContactForm.HEADING_ID;
  protected readonly categories = CONTACT_CATEGORIES;
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errors = signal<ContactFieldErrors>({});

  protected submit(event: Event): void {
    event.preventDefault();
    const form = new FormData(event.target as HTMLFormElement);
    const body: ContactRequest = { locale: this.page.locale() };
    for (const field of FIELDS) {
      const value = form.get(field);
      body[field] = typeof value === 'string' ? value : '';
    }
    void this.send(body);
  }

  private async send(body: ContactRequest): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    this.errors.set({});
    try {
      await firstValueFrom(sendContactMessage(this.http, this.apiOrigin, { body }));
      this.toasts.show('success', this.transloco.translate('contact.flash.sent'));
      this.ref.close(true);
    } catch (error) {
      this.refused(contactProblem(error));
    } finally {
      this.busy.set(false);
    }
  }

  private refused(problem: ContactProblem): void {
    if (problem.status === TOO_MANY_REQUESTS) {
      this.toasts.show('warning', this.transloco.translate('contact.flash.throttled'));
      return;
    }
    const errors = contactFieldErrors(problem.errors);
    this.errors.set(errors);
    if (Object.keys(errors).length === 0) {
      this.error.set('contact.flash.error');
    }
  }
}
