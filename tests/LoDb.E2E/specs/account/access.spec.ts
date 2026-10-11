import { expect, test } from '../../support/test';

// No account is created here: what a visitor meets on the account pages.
test.describe('account pages for a visitor', () => {
  test(
    'send the profile to the sign-in, which will bring the visitor back',
    { tag: '@readonly' },
    async ({ page, consoleErrors }) => {
      await page.goto('/en/account/profile');

      await expect(page).toHaveURL(/\/en\/account\/login\?/);
      expect(new URL(page.url()).searchParams.get('returnUrl')).toBe('/en/account/profile');
      await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();

      await test.step('draws the sign-in form, without console error', async () => {
        await expect(page.getByLabel('Email or summoner name')).toBeVisible();
        await expect(page.getByRole('link', { name: 'Create account' }).last()).toHaveAttribute(
          'href',
          '/en/account/register',
        );
        await page.waitForLoadState('networkidle');
        expect(consoleErrors).toEqual([]);
      });
    },
  );
});
