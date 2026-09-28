import { HttpStatusCode } from '@angular/common/http';
import type { AdminApiClientPage } from '../../../../../core/api/generated/models/admin-api-client-page';
import type { AdminApiClientRow } from '../../../../../core/api/generated/models/admin-api-client-row';
import type { AdminVisit } from '../../../testing/admin-visit';
import { openPanel } from '../../../testing/panels/open-panel';
import { press } from '../../../testing/dom/press';
import { reply } from '../../../testing/http/reply';
import { sent } from '../../../testing/http/sent';
import { submit } from '../../../testing/dom/submit';
import { notice } from '../../../testing/dom/notice';
import { ApiClientsPanel } from './api-clients-panel';

const CLIENTS = '/api/admin/api-clients';

function client(id: number, isActive = true): AdminApiClientRow {
  return {
    id,
    name: `Key ${id}`,
    keyPrefix: `lodb_${id}ab`,
    plan: id === 1 ? 'monthly' : 'free',
    isActive,
    revokedAt: isActive ? null : '2026-09-01T00:00:00Z',
    monthlyQuota: 100_000,
    usedThisMonth: 2_500,
    creditsBalance: 40,
    rateLimitPerMin: 60,
    createdAt: '2026-08-01T00:00:00Z',
    owner: { id: 3, username: 'dev', isBanned: false },
  };
}

function clientsPage(items = [client(1), client(2, false)]): AdminApiClientPage {
  return {
    items,
    page: 1,
    pages: 1,
    total: items.length,
    kpis: {
      active: 1,
      monthRequests: 2_500,
      credits: 40,
      byPlan: [
        { plan: 'monthly', keys: 3 },
        { plan: 'free', keys: 1 },
      ],
    },
    topConsumers: [{ id: 1, keyPrefix: 'lodb_1ab', username: 'dev', requests: 2_500 }],
  };
}

function row(visit: AdminVisit, id: number): Element {
  const found = visit.page.querySelector(`[data-api-client="${id}"]`);
  if (!found) {
    throw new Error(`No row for key ${id}.`);
  }
  return found;
}

function open() {
  return openPanel(ApiClientsPanel, '/admin/api-clients', [{ path: CLIENTS, body: clientsPage() }]);
}

describe('ApiClientsPanel', () => {
  it('rings the plans, ranks the heaviest keys and lists every key with its status', async () => {
    const visit = await open();

    const legend = [...visit.page.querySelectorAll('lodb-legend li')].map((li) =>
      li.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(legend).toEqual(['monthly 3', 'free 1']);
    expect(visit.page.querySelector('lodb-rank-list li')?.textContent).toContain('lodb_1ab… — dev');
    expect(row(visit, 1).textContent).toContain('2 500 / 100 000');
    expect(row(visit, 1).textContent).toContain('admin.api_clients.active');
    expect(row(visit, 1).querySelector('input[name="requests"]')?.getAttribute('max')).toBe(
      '1000000',
    );
    expect(row(visit, 2).textContent).toContain('admin.api_clients.revoked');
    expect(row(visit, 2).querySelector('button')).toBeNull();
    expect(row(visit, 2).querySelector('.actions')?.textContent?.trim()).toBe('—');
  });

  it('credits a key with the requests typed in its row, then reads the list again', async () => {
    const visit = await open();

    submit(visit, { requests: '5000' }, row(visit, 1));
    const credit = await sent(visit, `${CLIENTS}/1/credit`, 'POST');
    expect(credit.request.body).toEqual({ requests: 5000 });
    credit.flush({ creditsBalance: 5_040, rateLimitPerMin: 120 });
    await reply(visit, await sent(visit, CLIENTS), clientsPage());

    expect(notice()).toBe('notice: admin.api_clients.done.credit');
    expect(row(visit, 1).querySelector<HTMLInputElement>('input[name="requests"]')?.value).toBe('');
  });

  it('revokes a key once confirmed', async () => {
    const visit = await open();

    press(visit, 'admin.api_clients.revoke', row(visit, 1));
    press(visit, 'admin.api_clients.confirm_revoke', row(visit, 1));
    (await sent(visit, `${CLIENTS}/1/revoke`, 'POST')).flush('');
    await reply(visit, await sent(visit, CLIENTS), clientsPage([client(1, false)]));

    expect(notice()).toBe('notice: admin.api_clients.done.revoke');
    expect(row(visit, 1).textContent).toContain('admin.api_clients.revoked');
  });

  it('tells that a revoked key takes no credit', async () => {
    const visit = await open();

    submit(visit, { requests: '10' }, row(visit, 1));
    (await sent(visit, `${CLIENTS}/1/credit`, 'POST')).flush(
      { code: 'api-client-revoked' },
      { status: HttpStatusCode.Conflict, statusText: 'Conflict' },
    );

    await vi.waitFor(() => expect(notice()).toBe('alert: admin.errors.api_client_revoked'));
    expect(visit.page.querySelector('input[name="requests"]')).not.toBeNull();
  });
});
