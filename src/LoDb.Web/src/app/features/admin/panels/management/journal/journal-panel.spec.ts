import type { AuditEntryView } from '../../../../../core/api/generated/models/audit-entry-view';
import type { AuditPage } from '../../../../../core/api/generated/models/audit-page';
import type { AuditVocabularyView } from '../../../../../core/api/generated/models/audit-vocabulary-view';
import type { AuditVolume } from '../../../../../core/api/generated/models/audit-volume';
import type { AdminVisit } from '../../../testing/admin-visit';
import type { PanelCall } from '../../../testing/panels/panel-call';
import type { PanelVisit } from '../../../testing/panels/panel-visit';
import { openPanel } from '../../../testing/panels/open-panel';
import { press } from '../../../testing/dom/press';
import { reply } from '../../../testing/http/reply';
import { sent } from '../../../testing/http/sent';
import { submit } from '../../../testing/dom/submit';
import { notice } from '../../../testing/dom/notice';
import { JournalPanel } from './journal-panel';

const JOURNAL = '/api/admin/audit';
const VOLUME = '/api/admin/audit/volume';
const VOCABULARY = '/api/admin/audit/vocabulary';
const PURGE = '/api/admin/audit/purge';

const VOLUME_BODY: AuditVolume = {
  entries: 1_200,
  totalBytes: 3 * 1024 ** 2,
  oldest: '2026-03-01T00:00:00Z',
  newest: '2026-09-27T09:30:00Z',
  retentionMonths: 6,
  retentionCutoff: '2026-03-27T00:00:00Z',
};
const VOCABULARY_BODY: AuditVocabularyView = {
  categories: ['auth', 'admin'],
  outcomes: ['success', 'failure', 'denied'],
  actorTypes: ['user', 'admin', 'anonymous'],
  targetTypes: ['user'],
  actions: [
    { name: 'user.login', category: 'auth' },
    { name: 'user.logout', category: 'auth' },
    { name: 'admin.user.ban', category: 'admin' },
  ],
};

function entry(id: number, changes: Partial<AuditEntryView> = {}): AuditEntryView {
  return {
    id,
    action: 'admin.user.ban',
    category: 'admin',
    outcome: 'success',
    actorType: 'admin',
    actorId: 1,
    actor: 'root',
    targetType: 'user',
    targetId: '7',
    target: 'spammer',
    meta: { reason: 'spam' },
    occurredAt: '2026-09-27T09:30:00Z',
    ...changes,
  };
}

function journal(
  items = [
    entry(1),
    entry(2, { actorType: 'anonymous', actorId: null, actor: null, outcome: 'denied' }),
  ],
): AuditPage {
  return { items, page: 1, pageSize: 40, hasMore: true };
}

function open(url: string): Promise<PanelVisit> {
  const opening: PanelCall[] = [
    { path: VOLUME, body: VOLUME_BODY },
    { path: VOCABULARY, body: VOCABULARY_BODY },
    { path: JOURNAL, body: journal() },
  ];
  return openPanel(JournalPanel, url, opening);
}

function choose(visit: AdminVisit, radio: HTMLInputElement | null | undefined): void {
  radio?.click();
  visit.harness.detectChanges();
}

describe('JournalPanel', () => {
  it('weighs the journal and lists its entries, filters drawn from its vocabulary', async () => {
    const visit = await open('/admin/journal');

    const kpis = [...visit.page.querySelectorAll('lodb-kpi')].map((kpi) => kpi.textContent ?? '');
    expect(kpis[0]).toContain('1 200');
    expect(kpis[1]).toContain('3.00 MB');
    expect(kpis[2]).toContain('admin.journal.volume.months');
    const groups = [...visit.page.querySelectorAll('select[name="action"] optgroup')];
    expect(groups.map((group) => group.getAttribute('label'))).toEqual(['auth', 'admin']);
    expect(groups[0]?.querySelectorAll('option')).toHaveLength(2);
    const rows = [...visit.page.querySelectorAll('lodb-audit-table tbody tr')];
    expect(rows).toHaveLength(2);
    expect(rows[0]?.querySelector('a')?.getAttribute('href')).toBe('/admin/users/1/activity');
    const cells = [...(rows[0]?.querySelectorAll('td') ?? [])].map((cell) => cell.textContent);
    expect(cells[2]).toContain('admin · reason=spam');
    expect(cells[3]).toContain('spammer');
    expect(rows[1]?.querySelector('a')).toBeNull();
    expect(visit.page.querySelector('lodb-admin-pager a')?.getAttribute('href')).toBe(
      '/admin/journal?page=2',
    );
  });

  it('filters the entries through the URL, then clears the filters', async () => {
    const visit = await open('/admin/journal?page=4');
    const filters = visit.page.querySelector('lodb-journal-filters') ?? undefined;

    submit(
      visit,
      { category: 'auth', action: 'user.login', actor: ' root ', from: '2026-09-01' },
      filters,
    );
    const filtered = await sent(visit, JOURNAL);
    await reply(visit, filtered, journal([entry(3)]));

    const params = filtered.request.params;
    expect(params.getAll('action')).toEqual(['user.login']);
    expect(params.get('category')).toBe('auth');
    expect(params.get('actor')).toBe('root');
    expect(params.get('from')).toBe('2026-09-01');
    expect(params.get('page')).toBe('1');
    expect(params.has('outcome')).toBe(false);

    const reset = visit.page.querySelector<HTMLAnchorElement>('lodb-journal-filters a');
    expect(reset?.getAttribute('href')).toBe('/admin/journal');
    reset?.click();
    const cleared = await sent(visit, JOURNAL);
    await reply(visit, cleared, journal());
    expect(cleared.request.params.keys()).toEqual(['page']);
  });

  it('purges the entries before a day once confirmed, then weighs the journal again', async () => {
    const visit = await open('/admin/journal');
    const purge = visit.page.querySelector('lodb-journal-purge');

    choose(visit, purge?.querySelector<HTMLInputElement>('input[value="before"]'));
    expect(purge?.querySelector('button')?.disabled).toBe(true);
    const day = purge?.querySelector<HTMLInputElement>('input[type="date"]');
    if (day) {
      day.value = '2026-06-01';
      day.dispatchEvent(new Event('input'));
    }
    visit.harness.detectChanges();
    press(visit, 'admin.journal.purge.submit', purge ?? undefined);
    press(visit, 'admin.journal.purge.confirm', purge ?? undefined);
    const request = await sent(visit, PURGE, 'POST');
    expect(request.request.body).toEqual({ scope: 'before', before: '2026-06-01' });
    request.flush({ scope: 'before', deleted: 640, before: '2026-06-01T00:00:00Z' });
    // Both read again at once: the page settles only once both are answered.
    const volume = await sent(visit, VOLUME);
    const entries = await sent(visit, JOURNAL);
    volume.flush({ ...VOLUME_BODY, entries: 560 });
    await reply(visit, entries, journal());

    expect(notice()).toBe('notice: admin.journal.purged');
    expect(visit.page.querySelector('lodb-kpi')?.textContent).toContain('560');
  });

  it('purges what the retention would delete without asking a day', async () => {
    const visit = await open('/admin/journal');
    const purge = visit.page.querySelector('lodb-journal-purge') ?? undefined;

    expect(purge?.querySelector<HTMLInputElement>('input[value="retention"]')?.checked).toBe(true);
    press(visit, 'admin.journal.purge.submit', purge);
    press(visit, 'admin.journal.purge.confirm', purge);
    const request = await sent(visit, PURGE, 'POST');

    expect(request.request.body).toEqual({ scope: 'retention', before: null });
  });
});
