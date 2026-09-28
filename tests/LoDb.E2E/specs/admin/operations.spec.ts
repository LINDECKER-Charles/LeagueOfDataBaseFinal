import { expect, expectToast, openPanel, test } from './admin-test';

// A day before any entry of any stack: the purge it asks for deletes nothing.
const LONG_AGO = '2000-01-01';

test('shows the health of the API and reads it again on demand', async ({
  page,
  consoleErrors,
}) => {
  await openPanel(page, '/admin/monitoring', 'Surveillance');
  for (const heading of [
    'PostgreSQL',
    'Stockage',
    'Tables principales',
    "File d'e-mails",
    'Versions Data Dragon',
  ]) {
    await expect(page.getByRole('heading', { level: 2, name: heading })).toBeVisible();
  }
  await expect(page.getByText('opérationnel', { exact: true }).first()).toBeVisible();

  const reading = page.waitForResponse((response) => {
    const url = new URL(response.url());
    return url.pathname === '/api/admin/monitoring' && url.searchParams.get('refresh') === 'true';
  });
  await page.getByRole('button', { name: 'Rafraîchir' }).click();

  expect((await reading).status()).toBe(200);
  await expect(page.getByRole('button', { name: 'Rafraîchir' })).toBeEnabled();
  expect(consoleErrors).toEqual([]);
});

test('filters the journal, which the URL keeps, then clears the filters', async ({
  page,
  admin,
}) => {
  await openPanel(page, '/admin/journal', "Journal d'audit");
  await expect(page).toHaveTitle('Journal · Admin · LODB');
  const journal = page.getByRole('table');
  // A label holds its field, whose value or options may join its name: matched by its start.
  const filters = page.locator('lodb-journal-filters');

  await filters.getByLabel(/^Catégorie/).selectOption({ label: 'Authentification' });
  await filters.getByLabel(/^Action/).selectOption({ label: 'Connexion' });
  await filters.getByLabel(/^Acteur/).fill(admin.username);
  await filters.getByRole('button', { name: 'Filtrer', exact: true }).click();

  await expect(page).toHaveURL(/[?&]category=auth/);
  await expect(page).toHaveURL(/[?&]action=user\.login/);
  // The administrator signed in to enrol: the journal holds that sign-in at least.
  await expect(journal.getByRole('link', { name: admin.username }).first()).toBeVisible();
  await expect(journal.getByText('Connexion', { exact: true }).first()).toBeVisible();
  await expect(journal.getByText('Inscription', { exact: true })).toHaveCount(0);

  await page.reload();
  await expect(filters.getByLabel(/^Acteur/)).toHaveValue(admin.username);
  await filters.getByRole('link', { name: 'Réinitialiser' }).click();
  await expect(page).toHaveURL(/\/admin\/journal$/);
  await expect(filters.getByLabel(/^Catégorie/)).toHaveValue('');
});

test('purges the journal before a date, and journals the purge', async ({ page, admin }) => {
  await openPanel(page, '/admin/journal', "Journal d'audit");
  const maintenance = page.locator('lodb-journal-purge');

  await maintenance.getByRole('radio', { name: 'Avant le' }).check();
  await maintenance.locator('input[type="date"]').fill(LONG_AGO);
  await maintenance.getByRole('button', { name: 'Purger', exact: true }).click();
  const purge = page.waitForRequest(
    (request) => new URL(request.url()).pathname === '/api/admin/audit/purge',
  );
  await maintenance.getByRole('button', { name: 'Purger définitivement' }).click();

  expect((await purge).postDataJSON()).toEqual({ scope: 'before', before: LONG_AGO });
  await expectToast(page, 'Purge effectuée : 0 entrée(s) supprimée(s).');
  const actor = encodeURIComponent(admin.username);
  await openPanel(
    page,
    `/admin/journal?action=admin.logs_purge&actor=${actor}`,
    "Journal d'audit",
  );
  await expect(
    page.getByRole('table').getByText('Purge des journaux', { exact: true }).first(),
  ).toBeVisible();
});
