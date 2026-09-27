import { ChangeDetectionStrategy, Component } from '@angular/core';
import { deleteAdminContact } from '../../../../../core/api/generated/fn/admin-contacts/delete-admin-contact';
import { handleAdminContact } from '../../../../../core/api/generated/fn/admin-contacts/handle-admin-contact';
import { listAdminContacts } from '../../../../../core/api/generated/fn/admin-contacts/list-admin-contacts';
import { reopenAdminContact } from '../../../../../core/api/generated/fn/admin-contacts/reopen-admin-contact';
import type { AdminContactRow } from '../../../../../core/api/generated/models/admin-contact-row';
import { Button } from '../../../../../ui/controls/button';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { AdminCard } from '../../../layout/admin-card';
import { AdminRule } from '../../../layout/admin-rule';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectRowActions } from '../../../shared/http/inject-row-actions';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminPager } from '../../../widgets/admin-pager';
import { Badge, type Tone } from '../../../widgets/badge';
import { ConfirmButton } from '../../../widgets/confirm-button';
import { Kpi } from '../../../widgets/kpi';
import { type Segment, SegmentBar } from '../../../widgets/segment-bar';

/** The filters of the inbox: every message, then by status as the API names them. */
const STATUSES: readonly Segment[] = [
  { value: '', label: 'admin.contacts.statuses.all' },
  { value: 'new', label: 'admin.contacts.filters.new' },
  { value: 'handled', label: 'admin.contacts.filters.handled' },
];
const HANDLED = 'handled';
// The legacy tones of the categories: a bug is a failure, a business contact good news.
const CATEGORY_TONES: Readonly<Record<string, Tone>> = { bug: 'bad', commercial: 'good' };
// The legacy table cut a message there, its whole text in the title.
const EXCERPT_LENGTH = 160;

/**
 * `/admin/contacts`: the messages of the contact form, newest first, in the legacy table,
 * filtered by status. An administrator answers by e-mail (the contact's name opens it), marks
 * a message handled (or new again) and deletes it.
 */
@Component({
  selector: 'lodb-contacts-panel',
  imports: [
    AdminCard,
    AdminPager,
    AdminRule,
    Badge,
    Button,
    ConfirmButton,
    FigurePipe,
    Kpi,
    PageHead,
    PanelState,
    SegmentBar,
    StampPipe,
    AdminTextPipe,
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

  protected categoryTone(contact: AdminContactRow): Tone {
    return CATEGORY_TONES[contact.category] ?? 'muted';
  }

  protected excerpt(message: string): string {
    return message.length > EXCERPT_LENGTH ? `${message.slice(0, EXCERPT_LENGTH)}…` : message;
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
