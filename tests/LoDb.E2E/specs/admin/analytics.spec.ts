import { expect, expectToast, openPanel, test } from './admin-test';

// A fresh stack may have no traffic at all: the specs assert what every stack shows, never
// the figures themselves.
const ROLLED_UP = /^Consolidation terminée : \d+ journée\(s\) consolidée\(s\)\.$/;

test('shows the overview: the application, its traffic and its storage', async ({
  page,
  consoleErrors,
}) => {
  await openPanel(page, '/admin', "Vue d'ensemble");

  await expect(page).toHaveTitle("Vue d'ensemble · Admin · LODB");
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
  for (const heading of [
    'Fréquentation',
    'Par ressource',
    'Pages les plus consultées',
    'Sources de trafic',
    "Familles d'objets",
    'Déduplication',
    'Santé',
  ]) {
    await expect(page.getByRole('heading', { level: 2, name: heading })).toBeVisible();
  }
  await expect(page.getByText('Comptes', { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: /Services/ })).toHaveAttribute(
    'href',
    '/admin/monitoring',
  );
  expect(consoleErrors).toEqual([]);
});

test('switches the period of the traffic, which the URL keeps', async ({ page }) => {
  await openPanel(page, '/admin/traffic', 'Trafic');
  const periods = page.getByRole('navigation', { name: 'Période' });
  await expect(periods.getByRole('link', { name: '30 jours' })).toHaveAttribute(
    'aria-current',
    'page',
  );

  await periods.getByRole('link', { name: '7 jours' }).click();

  await expect(page).toHaveURL(/\/admin\/traffic\?range=7d$/);
  await expect(periods.getByRole('link', { name: '7 jours' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  await expect(page.getByText('Vues totales', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { level: 2, name: 'Rythme horaire' })).toBeVisible();
  await page.reload();
  await expect(periods.getByRole('link', { name: '7 jours' })).toHaveAttribute(
    'aria-current',
    'page',
  );
});

test('rolls the raw traffic up into the daily figures', async ({ page }) => {
  await openPanel(page, '/admin', "Vue d'ensemble");

  const rollup = page.waitForResponse(
    (response) => new URL(response.url()).pathname === '/api/admin/analytics/rollup',
  );
  await page.getByRole('button', { name: 'Consolider' }).click();

  expect((await rollup).ok()).toBe(true);
  await expectToast(page, ROLLED_UP);
});

test('shows the audience: devices, sources and the rankings', async ({ page, consoleErrors }) => {
  await openPanel(page, '/admin/audience?range=90d', 'Audience');

  for (const heading of [
    'Pays',
    'Appareils',
    'Navigateurs',
    'Systèmes',
    "Langue d'interface",
    'Sources de trafic',
    'Sites référents',
  ]) {
    await expect(page.getByRole('heading', { level: 2, name: heading })).toBeVisible();
  }
  await expect(
    page.getByRole('navigation', { name: 'Période' }).getByRole('link', { name: '90 jours' }),
  ).toHaveAttribute('aria-current', 'page');
  expect(consoleErrors).toEqual([]);
});

test('scans the storage again on demand', async ({ page }) => {
  await openPanel(page, '/admin/storage', 'Stockage');
  await expect(
    page.getByRole('heading', { level: 2, name: 'Ingestion dans le temps' }),
  ).toBeVisible();

  const scan = page.waitForResponse((response) => {
    const url = new URL(response.url());
    return url.pathname === '/api/admin/storage' && url.searchParams.get('refresh') === 'true';
  });
  await page.getByRole('button', { name: 'Rafraîchir' }).click();

  expect((await scan).status()).toBe(200);
  await expect(page.getByRole('button', { name: 'Rafraîchir' })).toBeEnabled();
  await expect(page.getByText('Poids total', { exact: true })).toBeVisible();
});
