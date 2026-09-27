import type { Page, Route } from '@playwright/test';
import { confirmAction, expect, expectToast, openPanel, test } from './admin-test';

// API keys and donations only come from Stripe, which the suite never calls: the actions on
// a key run against doubles of the admin API, the real panels are only read.

// A service worker would answer the calls before the page's routes see them.
test.use({ serviceWorkers: 'block' });

/** A key of the doubled API, as the list shows it; the actions change it. */
interface DoubledKey {
  readonly id: number;
  readonly name: string;
  creditsBalance: number;
  isActive: boolean;
  revokedAt: string | null;
}

interface KeyCalls {
  /** The bodies the page posted to credit the key, in order. */
  readonly credits: unknown[];
  /** How many times the page revoked the key. */
  revokes: number;
}

const NAME = 'Client E2E';
const MONTH_REQUESTS = 1_200;
const RATE_PER_MINUTE = 60;
const REVOKED = 204;

function keyPage(key: DoubledKey) {
  const owner = { id: 1, username: 'e2e_key_owner' };
  return {
    kpis: {
      active: key.isActive ? 1 : 0,
      monthRequests: MONTH_REQUESTS,
      credits: key.creditsBalance,
      byPlan: [{ plan: 'monthly', keys: 1 }],
    },
    topConsumers: [
      { id: key.id, keyPrefix: 'lodb_e2e', username: owner.username, requests: MONTH_REQUESTS },
    ],
    items: [
      {
        ...key,
        keyPrefix: 'lodb_e2e',
        plan: 'monthly',
        monthlyQuota: 100_000,
        usedThisMonth: MONTH_REQUESTS,
        rateLimitPerMin: RATE_PER_MINUTE,
        createdAt: '2026-09-01T10:00:00Z',
        owner,
      },
    ],
    total: 1,
    page: 1,
    pages: 1,
  };
}

// The admin API of the keys: the list, the credit and the revocation of one key.
async function doubleKeys(page: Page): Promise<KeyCalls> {
  const key: DoubledKey = {
    id: 9001,
    name: NAME,
    creditsBalance: 0,
    isActive: true,
    revokedAt: null,
  };
  const calls: KeyCalls = { credits: [], revokes: 0 };
  const answer = async (route: Route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() === 'GET') {
      return route.fulfill({ json: keyPage(key) });
    }
    if (path.endsWith('/credit')) {
      const body = request.postDataJSON() as { requests: number };
      calls.credits.push(body);
      key.creditsBalance += body.requests;
      return route.fulfill({
        json: { creditsBalance: key.creditsBalance, rateLimitPerMin: RATE_PER_MINUTE },
      });
    }
    calls.revokes += 1;
    key.isActive = false;
    key.revokedAt = '2026-09-27T12:00:00Z';
    return route.fulfill({ status: REVOKED });
  };
  await page.route((url) => url.pathname.startsWith('/api/admin/api-clients'), answer);
  return calls;
}

test('shows the keys of the stack, by plan and by consumption', async ({ page, consoleErrors }) => {
  await openPanel(page, '/admin/api-clients', 'Clients API');

  for (const label of ['Clés actives', 'Requêtes ce mois', 'Crédits en circulation']) {
    await expect(page.getByText(label, { exact: true })).toBeVisible();
  }
  for (const heading of ['Répartition par plan', 'Top consommateurs']) {
    await expect(page.getByRole('heading', { level: 2, name: heading })).toBeVisible();
  }
  await expect(page.getByRole('columnheader', { name: 'Conso mois' })).toBeVisible();
  expect(consoleErrors).toEqual([]);
});

test('credits a key with prepaid requests', async ({ page }) => {
  const calls = await doubleKeys(page);
  await openPanel(page, '/admin/api-clients', 'Clients API');
  const row = page.locator('tr[data-api-client]', { hasText: NAME });
  await expect(row.getByText('Mensuel', { exact: true })).toBeVisible();

  await row.getByLabel('Requêtes à créditer').fill('500');
  await row.getByRole('button', { name: 'Créditer', exact: true }).click();

  await expectToast(page, '500 requêtes créditées sur la clé lodb_e2e….');
  expect(calls.credits).toEqual([{ requests: 500 }]);
  await expect(row.getByRole('cell', { name: '500', exact: true })).toBeVisible();
  await expect(row.getByLabel('Requêtes à créditer')).toHaveValue('');
});

test('revokes a key', async ({ page }) => {
  const calls = await doubleKeys(page);
  await openPanel(page, '/admin/api-clients', 'Clients API');
  const row = page.locator('tr[data-api-client]', { hasText: NAME });

  await confirmAction(row, 'Révoquer', 'Confirmer la révocation');

  await expectToast(page, 'Clé lodb_e2e… révoquée (effective sous ~60 s côté API).');
  expect(calls.revokes).toBe(1);
  await expect(row.getByText('révoquée', { exact: true })).toBeVisible();
  await expect(row.getByRole('button', { name: 'Créditer', exact: true })).toHaveCount(0);
});

test('shows the donations of the stack, day by day', async ({ page, consoleErrors }) => {
  await openPanel(page, '/admin/donations', 'Dons');

  for (const label of [
    'Total collecté',
    '30 derniers jours',
    'Donateurs identifiés',
    'Supporters actifs',
  ]) {
    await expect(page.getByText(label, { exact: true })).toBeVisible();
  }
  await expect(page.getByText('Historique', { exact: true })).toBeVisible();
  await expect(page.getByRole('columnheader', { name: 'Compte lié' })).toBeVisible();
  expect(consoleErrors).toEqual([]);
});

test('shows each donation with its amount, its currency and its donor', async ({ page }) => {
  const donor = { id: 7, username: 'e2e_patron' };
  await page.route(
    (url) => url.pathname === '/api/admin/donations',
    (route) =>
      route.fulfill({
        json: {
          kpis: { totalCents: 3_750, count: 2, identifiedDonors: 1, anonymous: 1, supporters: 1 },
          daily: [{ date: '2026-09-26', cents: 3_750 }],
          items: [
            { id: 2, amountCents: 1_250, currency: 'usd', createdAt: '2026-09-26T18:00:00Z' },
            {
              id: 1,
              amountCents: 2_500,
              currency: 'eur',
              createdAt: '2026-09-26T09:30:00Z',
              donor,
              donorIsSupporter: true,
            },
          ].map((donation) => ({ donorIsSupporter: false, ...donation })),
          total: 2,
          page: 1,
          pages: 1,
        },
      }),
  );

  await openPanel(page, '/admin/donations', 'Dons');

  const foreign = page.locator('tr[data-donation="2"]');
  await expect(foreign).toContainText('12,50 USD');
  await expect(foreign).toContainText('anonyme');
  const identified = page.locator('tr[data-donation="1"]');
  await expect(identified).toContainText('25,00 EUR');
  await expect(identified.getByText(donor.username, { exact: true })).toBeVisible();
});
