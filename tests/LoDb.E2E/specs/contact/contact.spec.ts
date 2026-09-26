import type { Page } from '@playwright/test';
import { expectNoAccessibilityViolations } from '../../support/accessibility';
import { expect, test } from '../../support/test';

const HOME = '/en';
const CTA = 'Contact us';
const TITLE = 'A question, a bug, a project?';
const SENT = "Thanks! Your message has been sent — we'll get back to you soon.";

// The footer's call to action, the same on every page.
function openDialog(page: Page): Promise<void> {
  return page.getByRole('contentinfo').getByRole('button', { name: CTA }).click();
}

test('opens the contact form from the footer and closes it again', async ({
  page,
  consoleErrors,
}) => {
  await page.goto(HOME);

  await openDialog(page);
  const dialog = page.getByRole('dialog', { name: TITLE });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByLabel('Topic')).toHaveValue('bug');
  await expectNoAccessibilityViolations(page);

  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
  await openDialog(page);
  await dialog.getByRole('button', { name: 'Cancel' }).click();
  await expect(dialog).toHaveCount(0);
  expect(consoleErrors).toEqual([]);
});

// Two messages at most: the API takes five an hour from one address.
test('marks what the API refuses, then sends the message on a toast', async ({ page }) => {
  await page.goto(HOME);
  await openDialog(page);
  const dialog = page.getByRole('dialog', { name: TITLE });

  await test.step('a message too short is refused, what was typed kept', async () => {
    await dialog.getByLabel('Topic').selectOption({ label: 'Review' });
    await dialog.getByLabel('Email').fill('visitor@example.test');
    await dialog.getByLabel('Message').fill('Too short');
    await dialog.getByRole('button', { name: 'Send' }).click();
    await expect(
      dialog.getByText('Your message is too short (10 characters minimum).'),
    ).toBeVisible();
    await expect(dialog.getByLabel('Email')).toHaveValue('visitor@example.test');
  });

  await test.step('a valid message closes the dialog on a toast', async () => {
    await dialog.getByLabel('Message').fill('The rune pages no longer load since this morning.');
    await dialog.getByRole('button', { name: 'Send' }).click();
    await expect(page.getByText(SENT)).toBeVisible();
    await expect(dialog).toHaveCount(0);
  });
});
