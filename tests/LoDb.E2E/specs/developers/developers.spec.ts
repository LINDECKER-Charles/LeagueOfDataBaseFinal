import { expectNoAccessibilityViolations } from '../../support/accessibility';
import { readHead, typesOf } from '../../support/head';
import { expect, test } from '../../support/test';

const PAGE = '/en/developers';
const ROUTES = [
  '/healthz',
  '/v1/profiles/{username}',
  '/v1/champions/{championId}/builds',
  '/v1/trends/{type}',
  '/v1/usage',
];

test('documents the API on an indexable page, rendered by the server', async ({ page }) => {
  const response = await page.goto(PAGE);

  expect(response?.status()).toBe(200);
  await expect(
    page.getByRole('heading', { level: 1, name: 'The LeagueOfDataBase API' }),
  ).toBeVisible();
  const head = await readHead(page);
  expect(head.title.startsWith('API for developers')).toBe(true);
  expect(head.robots ?? '').not.toContain('noindex');
  expect(head.canonicals).toHaveLength(1);
  expect(typesOf(head.jsonLd).has('BreadcrumbList')).toBe(true);
  await expect(page.locator('.dev-endpoint code')).toHaveText(ROUTES);
  await expectNoAccessibilityViolations(page);
});

test('gives the configured base URL, never the legacy localhost', async ({ page }) => {
  await page.goto(PAGE);

  const baseUrl = page.getByTestId('developers-base-url');
  // The paragraph's text keeps the template's line breaks, collapsed to one space each side.
  await expect(baseUrl).toHaveText(/^\s*Base URL: https?:\/\/\S+\s*$/);
  const url = (await baseUrl.textContent())?.replace('Base URL:', '').trim() ?? '';
  expect(url).not.toContain('localhost:8090');
  await expect(page.getByTestId('developers-curl')).toContainText(`"${url}/v1/usage"`);
});

test('prices the free plan, the packs and the plans from the API', async ({ page }) => {
  await page.goto(PAGE);

  const offers = page.getByTestId('developers-pricing').locator('tbody tr');
  await expect(offers.first()).toHaveAttribute('data-offer', 'free');
  await expect(offers.first()).toContainText('500 req/month');
  await expect(page.locator('[data-offer="small"]')).toContainText('Pack S');
  await expect(page.locator('[data-offer="annual"]')).toContainText('/year');
  await expect(page.getByTestId('developers-usage')).toContainText('"monthly_quota": 500');
});

test('leads a reader to the portal, through the sign-in', async ({ page, consoleErrors }) => {
  await page.goto(PAGE);

  await page.getByTestId('developers-cta').click();

  await expect(page).toHaveURL(/\/en\/account\/login\?/);
  expect(new URL(page.url()).searchParams.get('returnUrl')).toBe('/en/account/api');
  expect(consoleErrors).toEqual([]);
});
