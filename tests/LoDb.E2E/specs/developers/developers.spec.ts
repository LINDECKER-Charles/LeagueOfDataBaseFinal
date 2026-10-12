import { expect, test } from '../../support/test';

// The page is rendered from the reference the real API publishes, where Vitest feeds it a
// double: the smoke of the feature on the stack. The prices, the routes and the page without
// a reference are developers-page.spec.ts's.
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

    await test.step('prices the plans from the API, the free one first', async () => {
      const offers = page.getByTestId('developers-pricing').locator('tbody tr');
      await expect(offers.first()).toHaveAttribute('data-offer', 'free');
    });
  },
);
