import type { AdminContactPage } from '../../../../../core/api/generated/models/admin-contact-page';
import type { AdminContactRow } from '../../../../../core/api/generated/models/admin-contact-row';
import type { AdminVisit } from '../../../testing/admin-visit';
import { openPanelInFrench } from '../../../testing/panels/open-panel-in-french';
import { openPanel } from '../../../testing/panels/open-panel';
import { press } from '../../../testing/dom/press';
import { reply } from '../../../testing/http/reply';
import { sent } from '../../../testing/http/sent';
import { notice } from '../../../testing/dom/notice';
import { ContactsPanel } from './contacts-panel';

const CONTACTS = '/api/admin/contacts';

function contact(id: number, status: string): AdminContactRow {
  return {
    id,
    status,
    category: 'bug',
    email: `player${id}@example.com`,
    name: `Player ${id}`,
    subject: id === 1 ? 'Broken page' : null,
    message: id === 1 ? 'x'.repeat(200) : 'The items page\nshows nothing.',
    locale: 'fr',
    createdAt: '2026-09-26T18:00:00Z',
    handledAt: status === 'handled' ? '2026-09-27T09:00:00Z' : null,
    user: id === 1 ? { id: 5, username: 'teemo', isBanned: false } : null,
  };
}

function inbox(items = [contact(1, 'new'), contact(2, 'handled')]): AdminContactPage {
  return {
    items,
    page: 1,
    pages: 1,
    total: items.length,
    stats: { total: 9, new: 4, handled: 5, week: 2 },
  };
}

function card(visit: AdminVisit, id: number): Element {
  const found = visit.page.querySelector(`[data-contact="${id}"]`);
  if (!found) {
    throw new Error(`No message ${id}.`);
  }
  return found;
}

function open(url = '/admin/contacts') {
  return openPanel(ContactsPanel, url, [{ path: CONTACTS, body: inbox() }]);
}

describe('ContactsPanel', () => {
  it('lists each message with its sender, its category, an excerpt and its status', async () => {
    const visit = await open();

    const cells = [...card(visit, 2).querySelectorAll('td')].map((cell) =>
      (cell.textContent ?? '').replace(/\s+/g, ' ').trim(),
    );
    expect(cells.slice(1, 5)).toEqual([
      'bug',
      'Player 2player2@example.com',
      'The items page shows nothing.',
      'handled',
    ]);
    expect(card(visit, 1).querySelector('strong')?.textContent).toBe('Broken page');
    const excerpt = card(visit, 1).querySelector('td:nth-child(4) span');
    expect(excerpt?.textContent).toBe(`${'x'.repeat(160)}…`);
    expect(excerpt?.getAttribute('title')).toBe('x'.repeat(200));
    expect(card(visit, 1).querySelector('a[href^="mailto:"]')?.getAttribute('href')).toBe(
      'mailto:player1@example.com',
    );
    expect(card(visit, 1).textContent).toContain('admin.contacts.account');
    expect(card(visit, 1).querySelector('span.inline-flex')?.className).toContain(
      'text-danger-light',
    );
    expect(card(visit, 1).textContent).toContain('admin.contacts.handle');
    expect(card(visit, 2).textContent).toContain('admin.contacts.reopen');
  });

  it('filters the inbox by status through the URL', async () => {
    const visit = await open('/admin/contacts?status=new');

    expect(visit.calls[0]?.request.params.get('status')).toBe('new');
    const current = visit.page.querySelector('lodb-segment-bar a[aria-current="page"]');
    expect(current?.textContent).toContain('admin.contacts.filters.new');
    const links = [...visit.page.querySelectorAll('lodb-segment-bar a')];
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/admin/contacts',
      '/admin/contacts?status=new',
      '/admin/contacts?status=handled',
    ]);
  });

  it('marks a new message handled, and a handled one new again', async () => {
    const visit = await open();

    press(visit, 'admin.contacts.handle', card(visit, 1));
    (await sent(visit, `${CONTACTS}/1/handle`, 'POST')).flush('');
    await reply(
      visit,
      await sent(visit, CONTACTS),
      inbox([contact(1, 'handled'), contact(2, 'handled')]),
    );
    expect(notice()).toBe('notice: admin.contacts.done.handle');
    press(visit, 'admin.contacts.reopen', card(visit, 2));
    (await sent(visit, `${CONTACTS}/2/reopen`, 'POST')).flush('');
    await reply(visit, await sent(visit, CONTACTS), inbox());

    expect(notice()).toBe('notice: admin.contacts.done.reopen');
  });

  it('deletes a message once confirmed', async () => {
    const visit = await open();

    press(visit, 'admin.actions.delete', card(visit, 2));
    press(visit, 'admin.contacts.confirm_delete', card(visit, 2));
    (await sent(visit, `${CONTACTS}/2`, 'DELETE')).flush('');
    await reply(visit, await sent(visit, CONTACTS), inbox([contact(1, 'new')]));

    expect(notice()).toBe('notice: admin.contacts.done.delete');
    expect(visit.page.querySelector('[data-contact="2"]')).toBeNull();
  });

  it('keeps its buttons in French once a message changes status, the site in English', async () => {
    const visit = await openPanelInFrench(ContactsPanel, '/admin/contacts', [
      { path: CONTACTS, body: inbox() },
    ]);
    expect(card(visit, 2).textContent).toContain('Rouvrir');

    press(visit, 'Marquer traité', card(visit, 1));
    (await sent(visit, `${CONTACTS}/1/handle`, 'POST')).flush('');
    await reply(
      visit,
      await sent(visit, CONTACTS),
      inbox([contact(1, 'handled'), contact(2, 'handled')]),
    );

    expect(card(visit, 1).textContent).toContain('Rouvrir');
    expect(card(visit, 1).textContent).toContain('Traité');
    expect(visit.page.textContent).not.toMatch(/Reopen|Handled/);
  });
});
