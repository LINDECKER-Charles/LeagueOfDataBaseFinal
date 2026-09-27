import { expectNoAccessibilityViolations } from '../../support/accessibility';
import { readHead } from '../../support/head';
import { createMember, signInMember } from '../../support/member-account';
import { expect, test } from '../../support/test';
import { discardAccount, type TestAccount } from '../account/accounts';
import { PORTAL, shownSecret, usageStatus } from './portal';

const OK = 200;
const FORBIDDEN = 403;
const STRIPE_PAGE = 'https://checkout.stripe.com/c/pay/cs_test_e2e_api';
const OFFERS = {
  available: true,
  currency: 'eur',
  packs: [{ code: 'small', priceCents: 500, requests: 5000 }],
  plans: [
    { code: 'monthly', interval: 'month', monthlyQuota: 15000, priceCents: 900, ratePerMinute: 60 },
  ],
};

// A service worker would answer the calls before the page's routes see them.
test.use({ serviceWorkers: 'block' });

// The account of the journey, deleted even when a step fails: no run leaves one behind. It is
// created by the CLI: the registration form's quota is account/register.spec.ts's.
let created: TestAccount | undefined;

test.afterEach(async ({ playwright, baseURL }) => {
  if (created) {
    await discardAccount(playwright.request, baseURL, created);
    created = undefined;
  }
});

test('sends a visitor to the sign-in, which brings them back', async ({ page }) => {
  await page.goto(PORTAL);

  await expect(page).toHaveURL(/\/en\/account\/login\?/);
  expect(new URL(page.url()).searchParams.get('returnUrl')).toBe(PORTAL);
});

test('asks an account to verify its e-mail before it issues a key', async ({ page, baseURL }) => {
  created = createMember('apig', { verified: false });
  await signInMember(page, baseURL, created);

  await page.goto(`${PORTAL}?status=cancelled`);

  await expect(page.getByRole('heading', { level: 1, name: 'API keys' })).toBeVisible();
  await expect(page.getByTestId('api-portal-notice')).toHaveText(
    'Payment cancelled — nothing was charged.',
  );
  await expect(
    page.getByText('Confirm your email address before generating an API key.'),
  ).toBeVisible();
  await expect(page.getByRole('link', { name: 'Verify my email' })).toHaveAttribute(
    'href',
    '/en/account/verify-email',
  );
  await expect(page.getByRole('button', { name: 'Create my key' })).toHaveCount(0);
  const head = await readHead(page);
  expect(head.robots).toContain('noindex');
});

test('issues, regenerates and revokes a key, which /v1 follows at once', async ({
  page,
  request,
  baseURL,
  consoleErrors,
}) => {
  created = createMember('apik', { verified: true });
  await signInMember(page, baseURL, created);
  await page.goto(PORTAL);
  let secret = '';

  await test.step('issues a key whose secret shows once', async () => {
    await page.getByLabel('Key name (optional)').fill('e2e-bot');
    await page.getByRole('button', { name: 'Create my key' }).click();
    secret = await shownSecret(page);
    await expect(page.getByTestId('api-key-prefix')).toHaveText(`${secret.slice(0, 12)}…`);
    await expect(page.getByTestId('api-key-plan')).toHaveText('Free');
    await expectNoAccessibilityViolations(page);
    expect(await usageStatus(request, secret)).toBe(OK);
  });

  await test.step('never shows the secret again', async () => {
    await page.reload();
    await expect(page.getByTestId('api-key-prefix')).toBeVisible();
    await expect(page.getByTestId('api-key-secret')).toHaveCount(0);
  });

  await test.step('regenerates it: the old secret is refused, the new one served', async () => {
    await page.getByRole('button', { name: 'Regenerate the key' }).click();
    await expect(page.getByTestId('api-portal-notice')).toHaveText(
      'Key regenerated — the previous secret no longer works.',
    );
    const renewed = await shownSecret(page);
    expect(renewed).not.toBe(secret);
    expect(await usageStatus(request, secret)).toBe(FORBIDDEN);
    expect(await usageStatus(request, renewed)).toBe(OK);
    secret = renewed;
    await page.getByRole('button', { name: 'I have copied it' }).click();
    await expect(page.getByTestId('api-key-secret')).toHaveCount(0);
  });

  await test.step('revokes it: /v1 refuses it from the next request', async () => {
    await page.getByRole('button', { name: 'Revoke', exact: true }).click();
    await expect(page.getByTestId('api-portal-notice')).toHaveText('Key revoked.');
    await expect(page.getByRole('button', { name: 'Create my key' })).toBeVisible();
    expect(await usageStatus(request, secret)).toBe(FORBIDDEN);
  });

  expect(consoleErrors).toEqual([]);
});

test('buys a credit pack through Stripe, in the page locale', async ({ page, baseURL }) => {
  created = createMember('apip', { verified: true });
  await signInMember(page, baseURL, created);
  // Stripe is a double: the API's offers and checkout are answered by the page's routes.
  const sent: unknown[] = [];
  await page.route('**/api/billing/offers', (route) => route.fulfill({ json: OFFERS }));
  await page.route('**/api/billing/checkout/pack', async (route) => {
    sent.push(route.request().postDataJSON());
    await route.fulfill({ json: { url: STRIPE_PAGE } });
  });
  await page.route('https://checkout.stripe.com/**', (route) =>
    route.fulfill({ contentType: 'text/html', body: '<!doctype html><title>Stripe</title>' }),
  );
  await page.goto(PORTAL);
  await page.getByRole('button', { name: 'Create my key' }).click();
  await shownSecret(page);

  await page.locator('[data-offer="small"]').getByRole('button', { name: 'Buy' }).click();

  await expect(page).toHaveURL(STRIPE_PAGE);
  expect(sent).toEqual([{ pack: 'small', locale: 'en' }]);
});
