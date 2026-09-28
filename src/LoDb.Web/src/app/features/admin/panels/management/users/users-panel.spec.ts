import { HttpStatusCode } from '@angular/common/http';
import type { AdminUserPage } from '../../../../../core/api/generated/models/admin-user-page';
import type { AdminUserRow } from '../../../../../core/api/generated/models/admin-user-row';
import { TestBed } from '@angular/core/testing';
import { AuthSession } from '../../../../../core/auth/session/auth-session';
import { AUTH_STRATEGY } from '../../../../../core/auth/strategy/auth-strategy-token';
import { accountUser } from '../../../../../core/auth/testing/account-user';
import { FakeAuthStrategy } from '../../../../../core/auth/testing/fake-auth-strategy';
import { configureAdminTestBed } from '../../../testing/admin-test-bed';
import { visitPanel } from '../../../testing/panels/visit-panel';
import type { AdminVisit } from '../../../testing/admin-visit';
import { press } from '../../../testing/dom/press';
import { reply } from '../../../testing/http/reply';
import { sent } from '../../../testing/http/sent';
import { submit } from '../../../testing/dom/submit';
import { notice } from '../../../testing/dom/notice';
import { UsersPanel } from './users-panel';

const USERS = '/api/admin/users';

function user(id: number, changes: Partial<AdminUserRow> = {}): AdminUserRow {
  return {
    id,
    username: `summoner${id}`,
    email: `summoner${id}@example.com`,
    emailVerified: true,
    google: false,
    isAdmin: false,
    isBanned: false,
    isPublicProfile: true,
    isSupporter: false,
    twoFactorEnabled: false,
    buildCount: id,
    createdAt: '2026-01-02T03:04:05Z',
    ...changes,
  };
}

function usersPage(items: readonly AdminUserRow[] = [user(1), user(2)]): AdminUserPage {
  return {
    items: [...items],
    page: 1,
    pages: 1,
    total: items.length,
    stats: { total: 5_400, newWeek: 37, banned: 2, supporters: 12 },
  };
}

function row(visit: AdminVisit, id: number): Element {
  const found = visit.page.querySelector(`[data-user="${id}"]`);
  if (!found) {
    throw new Error(`No row for account ${id}.`);
  }
  return found;
}

function badges(visit: AdminVisit, id: number): string[] {
  return [...row(visit, id).querySelectorAll('[lodbBadge]')].map(
    (badge) => badge.textContent?.trim() ?? '',
  );
}

// The panel reads who is signed in: a session without any account, unless a spec opens one.
function configure(): void {
  configureAdminTestBed(
    [{ path: 'admin/users', component: UsersPanel }],
    [{ provide: AUTH_STRATEGY, useValue: new FakeAuthStrategy() }],
  );
}

function open(url = '/admin/users', page = usersPage()) {
  configure();
  return visitPanel(url, [{ path: USERS, body: page }]);
}

// The list reads itself again after an action that succeeded.
async function reloaded(visit: AdminVisit, page = usersPage()): Promise<void> {
  await reply(visit, await sent(visit, USERS), page);
}

describe('UsersPanel', () => {
  it('lists the accounts with their counters and what sets them apart', async () => {
    const visit = await open(
      '/admin/users',
      usersPage([
        user(1, { isAdmin: true, twoFactorEnabled: true, riotTagline: 'EUW' }),
        user(2, { isBanned: true, banReason: 'spam', google: true, emailVerified: false }),
      ]),
    );

    expect(visit.page.querySelectorAll('lodb-kpi')[0]?.textContent).toContain('5 400');
    expect(row(visit, 1).querySelector('td')?.textContent?.trim()).toBe('summoner1#EUW');
    expect(badges(visit, 1)).toEqual([
      'admin.users.badges.public_profile',
      'admin.users.badges.admin',
    ]);
    expect(badges(visit, 2)).toEqual([
      'admin.users.badges.banned',
      'admin.users.badges.google',
      'admin.users.badges.public_profile',
      'admin.users.badges.unverified',
    ]);
    expect(row(visit, 2).querySelector('.cell-sub')?.textContent).toContain(
      'admin.users.ban_reason',
    );
    expect(row(visit, 2).querySelector('a')?.getAttribute('href')).toBe('/admin/users/2/activity');
    expect(row(visit, 2).textContent).toContain('admin.users.unban');
    expect(row(visit, 1).querySelector('input[name="reason"]')?.getAttribute('maxlength')).toBe(
      '255',
    );
  });

  it('offers the signed-in administrator the activity of their own account only', async () => {
    configure();
    TestBed.inject(AuthSession).apply({ user: accountUser({ id: 1, roles: ['Admin'] }) });
    const visit = await visitPanel('/admin/users', [{ path: USERS, body: usersPage() }]);

    expect(row(visit, 1).querySelectorAll('button')).toHaveLength(0);
    expect(row(visit, 1).querySelector('a')?.textContent).toContain('admin.users.activity');
    expect(row(visit, 2).querySelectorAll('button').length).toBeGreaterThan(0);
  });

  it('searches through the URL, from the first page', async () => {
    const visit = await open('/admin/users?page=3');

    expect(visit.calls[0]?.request.params.get('page')).toBe('3');
    submit(visit, { q: '  ahri ' });
    const search = await sent(visit, USERS);
    await reply(visit, search, usersPage([user(7)]));

    expect(search.request.params.get('q')).toBe('ahri');
    expect(search.request.params.get('page')).toBe('1');
    expect(visit.page.querySelector('[data-user="7"]')).not.toBeNull();
  });

  it('bans an account with a reason, then reads the list again', async () => {
    const visit = await open();

    submit(visit, { reason: ' spam ' }, row(visit, 2));
    const ban = await sent(visit, `${USERS}/2/ban`, 'POST');
    expect(ban.request.body).toEqual({ reason: 'spam' });
    ban.flush('');
    await reloaded(visit, usersPage([user(1), user(2, { isBanned: true })]));

    expect(notice()).toBe('notice: admin.users.done.ban');
    expect(row(visit, 2).textContent).toContain('admin.users.badges.banned');
    expect(row(visit, 2).querySelector('input[name="reason"]')).toBeNull();
  });

  it('lifts a ban at once, as the legacy admin did', async () => {
    const visit = await open('/admin/users', usersPage([user(3, { isBanned: true })]));

    press(visit, 'admin.users.unban', row(visit, 3));
    (await sent(visit, `${USERS}/3/unban`, 'POST')).flush('');
    await reloaded(visit);

    expect(notice()).toBe('notice: admin.users.done.unban');
  });

  it('deletes an account once confirmed', async () => {
    const visit = await open();

    press(visit, 'admin.actions.delete', row(visit, 1));
    press(visit, 'admin.users.confirm_delete', row(visit, 1));
    (await sent(visit, `${USERS}/1`, 'DELETE')).flush('');
    await reloaded(visit, usersPage([user(2)]));

    expect(notice()).toBe('notice: admin.users.done.delete');
    expect(visit.page.querySelector('[data-user="1"]')).toBeNull();
  });

  it('tells why the API refused to ban oneself, and keeps the list', async () => {
    const visit = await open();

    submit(visit, { reason: '' }, row(visit, 1));
    const ban = await sent(visit, `${USERS}/1/ban`, 'POST');
    expect(ban.request.body).toEqual({ reason: null });
    ban.flush(JSON.stringify({ code: 'self-moderation' }), {
      status: HttpStatusCode.Conflict,
      statusText: 'Conflict',
    });

    await vi.waitFor(() => expect(notice()).toBe('alert: admin.errors.self_moderation'));
    visit.http.expectNone((request) => request.url.endsWith(USERS));
    expect(visit.page.querySelector('input[name="reason"]')).not.toBeNull();
  });
});
