import { expect, test } from '../../support/test';

// The way in to a donation on the real stack: the header's link, then the form as the API's
// options open or close it, read by the page's resolver. The form itself, its amounts and the
// way to Stripe, are donate-page.spec.ts's, the checkout the API's.
interface DonationOptions {
  readonly available: boolean;
}

test(
  'leads from the header to the donation page, as the API opens or closes it',
  { tag: '@readonly' },
  async ({ page, request, consoleErrors }) => {
    const answer = await request.get('/api/donations/options');
    const options = (await answer.json()) as DonationOptions;
    await page.goto('/en');

    await page.getByRole('banner').getByRole('link', { name: 'Donate' }).click();

    await expect(page).toHaveURL(/\/en\/donate$/);
    await expect(
      page.getByRole('heading', { level: 1, name: 'Support League Of Data Base' }),
    ).toBeVisible();
    // Disabled while the API has no Stripe account, as on a local stack.
    await expect(page.getByRole('button', { name: 'Donate with Stripe' })).toBeEnabled({
      enabled: options.available,
    });
    expect(consoleErrors).toEqual([]);
  },
);
