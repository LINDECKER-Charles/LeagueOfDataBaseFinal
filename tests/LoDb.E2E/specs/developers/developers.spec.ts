import { expect, test } from '../../support/test';

const PAGE = '/en/developers';

test(
  'documents the API on an indexable page, rendered by the server',
  { tag: '@readonly' },
  async ({ page }) => {
    const response = await page.goto(PAGE);

    expect(response?.status()).toBe(200);
    await expect(
      page.getByRole('heading', { level: 1, name: 'The LeagueOfDataBase API' }),
    ).toBeVisible();

    await test.step('prices the free plan, the packs and the plans from the API', async () => {
      const offers = page.getByTestId('developers-pricing').locator('tbody tr');
      await expect(offers.first()).toHaveAttribute('data-offer', 'free');
      await expect(offers.first()).toContainText('500 req/month');
      await expect(page.locator('[data-offer="small"]')).toContainText('Pack S');
      await expect(page.locator('[data-offer="annual"]')).toContainText('/year');
      await expect(page.getByTestId('developers-usage')).toContainText('"monthly_quota": 500');
    });
  },
);
