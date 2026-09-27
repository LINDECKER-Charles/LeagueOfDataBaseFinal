import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { deleteAdminContact } from '../../../../../core/api/generated/fn/admin-contacts/delete-admin-contact';
import { handleAdminContact } from '../../../../../core/api/generated/fn/admin-contacts/handle-admin-contact';
import { listAdminContacts } from '../../../../../core/api/generated/fn/admin-contacts/list-admin-contacts';
import { reopenAdminContact } from '../../../../../core/api/generated/fn/admin-contacts/reopen-admin-contact';
import type { AdminContactRow } from '../../../../../core/api/generated/models/admin-contact-row';
import { Button } from '../../../../../ui/controls/button';
import { Frame } from '../../../../../ui/surfaces/frame';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { injectRowActions } from '../../../shared/http/inject-row-actions';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminPager } from '../../../widgets/admin-pager';
import { Badge } from '../../../widgets/badge';
import { ConfirmButton } from '../../../widgets/confirm-button';
import { Kpi } from '../../../widgets/kpi';
import { PageHead } from '../../../widgets/page-head';

/** The filters of the inbox: every message, then by status as the API names them. */
const STATUSES = ['', 'new', 'handled'] as const;
const HANDLED = 'handled';

/**
 * `/admin/contacts`: the messages of the contact form, newest first. An administrator
 * answers by e-mail, marks a message handled (or new again) and deletes it.
 */
@Component({
  selector: 'lodb-contacts-panel',
  imports: [
    AdminPager,
    Badge,
    Button,
    ConfirmButton,
    FigurePipe,
    Frame,
    Kpi,
    PageHead,
    PanelState,
    RouterLink,
    StampPipe,
    TranslocoPipe,
  ],
  templateUrl: './contacts-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContactsPanel {
  protected readonly texts = injectAdminText();
  protected readonly query = injectQuery();
  protected readonly statuses = STATUSES;
  protected readonly contacts = injectPanel(listAdminContacts, () => ({
    status: this.query.text('status') || undefined,
    page: this.query.page(),
  }));
  protected readonly actions = injectRowActions(() => this.contacts.reload());

  protected isHandled(contact: AdminContactRow): boolean {
    return contact.status === HANDLED;
  }

  protected toggle(contact: AdminContactRow): void {
    const handled = this.isHandled(contact);
    void this.actions.run(contact.id, {
      call: handled ? reopenAdminContact : handleAdminContact,
      params: { id: contact.id },
      done: { key: handled ? 'contacts.done.reopen' : 'contacts.done.handle' },
    });
  }

  protected remove(contact: AdminContactRow): void {
    void this.actions.run(contact.id, {
      call: deleteAdminContact,
      params: { id: contact.id },
      done: { key: 'contacts.done.delete' },
    });
  }
}
