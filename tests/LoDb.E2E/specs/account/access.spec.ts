import { expect, test } from '../../support/test';

// No account is created here: what a visitor meets on the account pages. They render in the
// browser alone, behind a guard that asks the real API for the session: only the stack shows
// the visitor landing on the sign-in, the way back kept. The guard's rules are Vitest's.
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
        // The registration keeps the way back too.
        await expect(page.getByRole('link', { name: 'Create account' }).last()).toHaveAttribute(
          'href',
          '/en/account/register?returnUrl=%2Fen%2Faccount%2Fprofile',
        );
        await page.waitForLoadState('networkidle');
        expect(consoleErrors).toEqual([]);
      });
    },
  );
});
