import type { Page } from '@playwright/test';
import { expectNoAccessibilityViolations } from '../../support/accessibility';
import { readHead } from '../../support/head';
import { expect, test } from '../../support/test';

// Stripe's hosted page, never reached: the API's answers are doubles, so no session is opened.
const STRIPE_PAGE = 'https://checkout.stripe.com/c/pay/cs_test_e2e';
const OPTIONS = {
  available: true,
  currency: 'eur',
  presets: [300, 500, 1000, 2500],
  minCents: 100,
  maxCents: 50000,
};

// A service worker would answer the calls before the page's routes see them.
test.use({ serviceWorkers: 'block' });

interface Checkout {
  /** The bodies the page posted to open a checkout, in order. */
  readonly sent: unknown[];
}

// The API opens the form and a session; Stripe's page is a stub the browser lands on.
async function doubleStripe(page: Page): Promise<Checkout> {
  const sent: unknown[] = [];
  await page.route('**/api/donations/options', (route) => route.fulfill({ json: OPTIONS }));
  await page.route('**/api/donations/checkout', async (route) => {
    sent.push(route.request().postDataJSON());
    await route.fulfill({ json: { url: STRIPE_PAGE } });
  });
  await page.route('https://checkout.stripe.com/**', (route) =>
    route.fulfill({
      contentType: 'text/html',
      body: '<!doctype html><html lang="en"><title>Stripe Checkout</title><h1>Pay</h1></html>',
    }),
  );
  return { sent };
}

// From the home page, through the header: the page and its options load in the browser.
async function openDonatePage(page: Page): Promise<void> {
  await page.goto('/en');
  await page.getByRole('banner').getByRole('link', { name: 'Donate' }).click();
  await expect(page).toHaveURL(/\/en\/donate$/);
  await expect(
    page.getByRole('heading', { level: 1, name: 'Support League Of Data Base' }),
  ).toBeVisible();
}

test('sends a donor to Stripe with the tier they chose', { tag: '@readonly' }, async ({
  page,
  consoleErrors,
}) => {
  const checkout = await doubleStripe(page);
  await openDonatePage(page);
  await expect(page.getByRole('radio', { name: /Gemstone/ })).toBeChecked();
  await expectNoAccessibilityViolations(page);

  await page.getByRole('radio', { name: /Crest/ }).check();
  await page.getByRole('button', { name: 'Donate with Stripe' }).click();

  await expect(page).toHaveURL(STRIPE_PAGE);
  expect(checkout.sent).toEqual([{ amountCents: 1000, locale: 'en' }]);
  expect(consoleErrors).toEqual([]);
});

test('gives a free amount over the tier, and refuses one out of bounds', { tag: '@readonly' }, async ({
  page,
}) => {
  const checkout = await doubleStripe(page);
  await openDonatePage(page);
  const amount = page.getByLabel('Free amount');
  const donate = page.getByRole('button', { name: 'Donate with Stripe' });

  await test.step('an amount above 500 € stays on the form', async () => {
    await amount.fill('600');
    await donate.click();
    await expect(page.getByRole('alert')).toHaveText(
      'Invalid amount — enter between 1 € and 500 €.',
    );
    expect(checkout.sent).toEqual([]);
  });

  await test.step('7,50 € leaves for Stripe as 750 cents', async () => {
    await amount.fill('7,50');
    await donate.click();
    await expect(page).toHaveURL(STRIPE_PAGE);
    expect(checkout.sent).toEqual([{ amountCents: 750, locale: 'en' }]);
  });
});

test('keeps the return pages out of the index, their links followed', { tag: '@readonly' }, async ({
  page,
  consoleErrors,
}) => {
  for (const [path, title] of [
    ['/en/donate/success?session_id=cs_test_e2e', 'Thank you, Summoner'],
    ['/en/donate/cancel', 'Donation cancelled'],
  ] as const) {
    await page.goto(path);
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();
    const head = await readHead(page);
    expect(head.robots).toBe('noindex, follow');
    expect(head.title.startsWith(`${title} · `)).toBe(true);
    expect(head.alternates).toEqual([]);
  }
  await page.getByRole('link', { name: 'Try again' }).click();
  await expect(page).toHaveURL(/\/en\/donate$/);
  expect(consoleErrors).toEqual([]);
});
