import type { AdminDonationPage } from '../../../../../core/api/generated/models/admin-donation-page';
import { openPanel } from '../../../testing/panels/open-panel';
import { DonationsPanel } from './donations-panel';

const DONATIONS = '/api/admin/donations';

const PAGE: AdminDonationPage = {
  page: 2,
  pages: 3,
  total: 45,
  kpis: { totalCents: 123_456, count: 45, identifiedDonors: 30, anonymous: 15, supporters: 8 },
  daily: [
    { date: '2026-09-25', cents: 500 },
    { date: '2026-09-26', cents: 0 },
    { date: '2026-09-27', cents: 2_000 },
  ],
  items: [
    {
      id: 1,
      amountCents: 2_000,
      currency: 'eur',
      createdAt: '2026-09-27T12:00:00Z',
      donor: { id: 4, username: 'lux' },
      donorIsSupporter: true,
    },
    {
      id: 2,
      amountCents: 500,
      currency: 'USD',
      createdAt: '2026-09-25T08:00:00Z',
      donorIsSupporter: false,
    },
  ],
};

function cells(page: HTMLElement, id: number): string[] {
  return [...page.querySelectorAll(`[data-donation="${id}"] td`)].map((td) =>
    (td.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

describe('DonationsPanel', () => {
  it('totals the gifts, charts them day by day and lists them page by page', async () => {
    const { page, calls } = await openPanel(DonationsPanel, '/admin/donations?page=2', [
      { path: DONATIONS, body: PAGE },
    ]);

    expect(calls[0]?.request.params.get('page')).toBe('2');
    expect(page.querySelector('lodb-kpi')?.textContent).toContain('1 234,56 €');
    expect(page.querySelectorAll('lodb-time-series-chart polyline.ts-line')).toHaveLength(1);
    expect(page.querySelectorAll('lodb-time-series-chart .ts-axis')[2]?.textContent?.trim()).toBe(
      '20€',
    );
    expect(cells(page, 1)).toEqual([
      '27/09/2026 12:00',
      '20,00 €',
      // A flex row sets the name and the badge apart, not a space.
      'luxadmin.users.badges.supporter',
    ]);
    expect(cells(page, 2)).toEqual(['25/09/2026 08:00', '5,00 € USD', 'admin.donations.anonymous']);
    const pager = [...page.querySelectorAll('lodb-admin-pager a')].map((a) =>
      a.getAttribute('href'),
    );
    expect(pager).toEqual(['/admin/donations?page=1', '/admin/donations?page=3']);
  });
});
