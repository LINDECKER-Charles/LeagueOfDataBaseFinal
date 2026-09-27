import type { AuditEntryView } from '../../../../../core/api/generated/models/audit-entry-view';
import type { UserActivity } from '../../../../../core/api/generated/models/user-activity';
import { openPanel } from '../../../testing/panels/open-panel';
import { UserActivityPanel } from './user-activity-panel';

const ACTIVITY = '/api/admin/audit/users/42';
const ROUTE = { path: 'admin/users/:id/activity', component: UserActivityPanel };

function entry(id: number, changes: Partial<AuditEntryView> = {}): AuditEntryView {
  return {
    id,
    action: 'user.login',
    category: 'auth',
    outcome: 'success',
    actorType: 'user',
    actorId: 42,
    actor: 'ahri',
    occurredAt: '2026-09-27T08:30:00Z',
    ip: '203.0.113.9',
    route: '/api/auth/login',
    ...changes,
  };
}

function activity(changes: Partial<UserActivity> = {}): UserActivity {
  return {
    subject: { id: 42, username: 'ahri', email: 'ahri@example.com' },
    activity: {
      items: [entry(1), entry(2, { outcome: 'failure' })],
      page: 2,
      pageSize: 40,
      hasMore: true,
    },
    ...changes,
  };
}

describe('UserActivityPanel', () => {
  it('shows what an account did, newest first, page by page', async () => {
    const { page, calls } = await openPanel(ROUTE, '/admin/users/42/activity?page=2', [
      { path: ACTIVITY, body: activity() },
    ]);

    expect(calls[0]?.request.params.get('page')).toBe('2');
    expect(page.querySelector('h1')?.textContent).toContain('admin.activity.title');
    expect(page.textContent).toContain('ahri@example.com');
    expect(page.querySelectorAll('tbody tr')).toHaveLength(2);
    expect(page.querySelector('tbody tr td')?.textContent?.trim()).toBe('27/09/2026 08:30:00');
    const pager = [...page.querySelectorAll('lodb-admin-pager a')].map((a) =>
      a.getAttribute('href'),
    );
    expect(pager).toEqual(['/admin/users/42/activity?page=1', '/admin/users/42/activity?page=3']);
  });

  it('names an account the journal no longer knows by its id', async () => {
    const { page } = await openPanel(ROUTE, '/admin/users/42/activity', [
      {
        path: ACTIVITY,
        body: activity({
          subject: null,
          activity: { items: [], page: 1, pageSize: 40, hasMore: false },
        }),
      },
    ]);

    expect(page.textContent).toContain('admin.journal.empty');
    expect(page.querySelector('lodb-admin-pager nav')).toBeNull();
  });
});
