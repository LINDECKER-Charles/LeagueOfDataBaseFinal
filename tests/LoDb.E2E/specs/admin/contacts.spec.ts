import type { Page } from '@playwright/test';
import { confirmAction, expect, expectToast, NO_SESSION, openPanel, test } from './admin-test';

// One message for the whole journey, whose steps follow one another: the API takes five
// messages an hour from one address, and the contact spec sends two of them.
test.describe.configure({ mode: 'serial' });

const SUBJECT = `Retour E2E ${Date.now().toString(36)}`;
const SENDER = 'e2e-visitor@example.test';
const SENT = 204;

test.beforeAll(async ({ playwright }, testInfo) => {
  const baseURL = testInfo.project.use.baseURL;
  // A visitor without a session: the API asks for the site's origin, and no XSRF token.
  // The session of the administrator, which a request context would take, asks for one.
  const visitor = await playwright.request.newContext({
    baseURL,
    storageState: NO_SESSION,
    extraHTTPHeaders: { Origin: new URL(baseURL ?? '').origin },
  });
  try {
    const sent = await visitor.post('/api/contact', {
      data: {
        category: 'feedback',
        name: 'Visiteur E2E',
        email: SENDER,
        subject: SUBJECT,
        message: 'Les pages de runes se chargent bien, merci pour le site.',
        locale: 'fr',
      },
    });
    expect(sent.status(), 'the API takes the message of the journey').toBe(SENT);
  } finally {
    await visitor.dispose();
  }
});

function card(page: Page) {
  return page.locator('tr[data-contact]', { hasText: SUBJECT });
}

test('lists a new message, which the statuses sort', async ({ page }) => {
  await openPanel(page, '/admin/contacts', 'Messages de contact');
  await expect(page).toHaveTitle('Messages · Admin · LODB');
  await expect(card(page).getByText('Suggestion / feedback', { exact: true })).toBeVisible();
  await expect(card(page).getByRole('link', { name: 'Visiteur E2E' })).toHaveAttribute(
    'href',
    `mailto:${SENDER}`,
  );
  await expect(card(page).getByText('Nouveau', { exact: true })).toBeVisible();
  const statuses = page.getByRole('navigation', { name: 'Filtrer par statut' });

  await statuses.getByRole('link', { name: 'Nouveaux' }).click();
  await expect(page).toHaveURL(/[?&]status=new/);
  await expect(card(page)).toHaveCount(1);

  await statuses.getByRole('link', { name: 'Traités' }).click();
  await expect(page).toHaveURL(/[?&]status=handled/);
  await expect(card(page)).toHaveCount(0);
});

test('marks a message handled, then reopens it', async ({ page }) => {
  await openPanel(page, '/admin/contacts', 'Messages de contact');

  await card(page).getByRole('button', { name: 'Marquer traité' }).click();
  await expectToast(page, 'Message marqué comme traité.');
  await expect(card(page).getByText('Traité', { exact: true })).toBeVisible();

  await card(page).getByRole('button', { name: 'Rouvrir' }).click();
  await expectToast(page, 'Message rouvert.');
  await expect(card(page).getByRole('button', { name: 'Marquer traité' })).toBeVisible();
});

test('deletes a message', async ({ page }) => {
  await openPanel(page, '/admin/contacts?status=new', 'Messages de contact');

  await confirmAction(card(page), 'Supprimer', 'Supprimer définitivement');

  await expectToast(page, 'Message supprimé définitivement.');
  await expect(card(page)).toHaveCount(0);
});
