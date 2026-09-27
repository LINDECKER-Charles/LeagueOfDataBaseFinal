import { expect, expectToast, openPanel, test } from './admin-test';

// A day before any entry of any stack: the purge it asks for deletes nothing.
const LONG_AGO = '2000-01-01';

test('shows the health of the API and reads it again on demand', async ({
  page,
  consoleErrors,
}) => {
  await openPanel(page, '/admin/monitoring', 'Surveillance');
  const services = page.getByRole('table').first();
  await expect(services.getByRole('columnheader', { name: 'Latence' })).toBeVisible();
  await expect(services.getByRole('cell', { name: 'postgres' })).toBeVisible();
  for (const heading of ["Processus de l'API", 'Ingestion', 'Versions Data Dragon']) {
    await expect(page.getByRole('heading', { level: 2, name: heading })).toBeVisible();
  }

  const reading = page.waitForResponse((response) => {
    const url = new URL(response.url());
    return url.pathname === '/api/admin/monitoring' && url.searchParams.get('refresh') === 'true';
  });
  await page.getByRole('button', { name: 'Actualiser' }).click();

  expect((await reading).status()).toBe(200);
  await expect(page.getByText(/^Relevé du /)).toBeVisible();
  expect(consoleErrors).toEqual([]);
});

test('filters the journal, which the URL keeps, then clears the filters', async ({
  page,
  admin,
}) => {
  await openPanel(page, '/admin/journal', 'Journal');
  const journal = page.getByRole('table');
  // A label holds its field, whose value or options may join its name: matched by its start.
  const filters = page.locator('lodb-journal-filters');

  await filters.getByLabel(/^Catégorie/).selectOption({ label: 'Authentification' });
  await filters.getByLabel(/^Action/).selectOption({ label: 'Connexion' });
  await filters.getByLabel(/^Auteur/).fill(admin.username);
  await filters.getByRole('button', { name: 'Filtrer', exact: true }).click();

  await expect(page).toHaveURL(/[?&]category=auth/);
  await expect(page).toHaveURL(/[?&]action=user\.login/);
  // The administrator signed in to enrol: the journal holds that sign-in at least.
  await expect(journal.getByRole('link', { name: admin.username }).first()).toBeVisible();
  await expect(journal.getByText('Connexion', { exact: true }).first()).toBeVisible();
  await expect(journal.getByText('Inscription', { exact: true })).toHaveCount(0);

  await page.reload();
  await expect(filters.getByLabel(/^Auteur/)).toHaveValue(admin.username);
  await page.getByRole('button', { name: 'Effacer les filtres' }).click();
  await expect(page).toHaveURL(/\/admin\/journal$/);
  await expect(filters.getByLabel(/^Catégorie/)).toHaveValue('');
});

test('purges the journal before a date, and journals the purge', async ({ page, admin }) => {
  await openPanel(page, '/admin/journal', 'Journal');
  const maintenance = page.locator('lodb-journal-purge');

  await maintenance.getByLabel(/^Portée/).selectOption({ label: 'Avant une date' });
  await maintenance.getByLabel(/^Avant le/).fill(LONG_AGO);
  await maintenance.getByRole('button', { name: 'Purger', exact: true }).click();
  const purge = page.waitForRequest(
    (request) => new URL(request.url()).pathname === '/api/admin/audit/purge',
  );
  await maintenance.getByRole('button', { name: 'Confirmer la purge' }).click();

  expect((await purge).postDataJSON()).toEqual({ scope: 'before', before: LONG_AGO });
  await expectToast(page, 'Aucune entrée supprimée.');
  const actor = encodeURIComponent(admin.username);
  await openPanel(page, `/admin/journal?action=admin.logs_purge&actor=${actor}`, 'Journal');
  await expect(
    page.getByRole('table').getByText('Purge des journaux', { exact: true }).first(),
  ).toBeVisible();
});
