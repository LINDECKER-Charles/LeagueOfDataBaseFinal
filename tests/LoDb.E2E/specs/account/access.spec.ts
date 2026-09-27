import { expect, test } from '../../support/test';
import { signIn } from './accounts';

const PASSWORD_RULES = 6;

// No account is created here: what a visitor meets on the account pages.
test.describe('account pages for a visitor', () => {
  test('are rendered in the browser, never cached nor indexed', { tag: '@readonly' }, async ({
    request,
  }) => {
    const response = await request.get('/en/account/login');

    expect(response.status()).toBe(200);
    expect(response.headers()['cache-control']).toContain('no-store');
    expect(response.headers()['x-robots-tag']).toContain('noindex');
    expect(await response.text()).not.toContain('ng-server-context="ssr"');
  });

  test('send the profile to the sign-in, which will bring the visitor back', { tag: '@readonly' }, async ({
    page,
  }) => {
    await page.goto('/en/account/profile');

    await expect(page).toHaveURL(/\/en\/account\/login\?/);
    expect(new URL(page.url()).searchParams.get('returnUrl')).toBe('/en/account/profile');
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
  });

  test('draw the sign-in form, without console error', { tag: '@readonly' }, async ({
    page,
    consoleErrors,
  }) => {
    await page.goto('/en/account/login');

    await expect(page.getByLabel('Email or summoner name')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Create account' }).last()).toHaveAttribute(
      'href',
      '/en/account/register',
    );
    await page.waitForLoadState('networkidle');
    expect(consoleErrors).toEqual([]);
  });

  test('say plainly that unknown credentials are refused', async ({ page }) => {
    await page.goto('/en/account/login');

    await signIn(page, 'nobody-e2e@example.com', 'Not-the-right-one-42');

    await expect(page.getByRole('alert')).toHaveText('Incorrect identifier or password.');
    await expect(page).toHaveURL(/\/en\/account\/login$/);
  });

  test('check a new password as it is typed, with the rules of the server', { tag: '@readonly' }, async ({
    page,
  }) => {
    await page.goto('/en/account/register');
    const rules = page.locator('lodb-password-checklist li');

    await page.getByLabel('Password', { exact: true }).fill('short');
    await expect(rules).toHaveCount(PASSWORD_RULES);
    await expect(rules.filter({ hasText: '(met)' })).toHaveCount(1);

    await page.getByLabel('Password', { exact: true }).fill('Hextech-Forge-2046!');
    await page.getByLabel('Confirm password').fill('Hextech-Forge-2046!');
    await expect(rules.filter({ hasText: '(met)' })).toHaveCount(PASSWORD_RULES);
  });

  test('keep a mismatched confirmation from being sent', { tag: '@readonly' }, async ({ page }) => {
    await page.goto('/en/account/register');
    let sent = false;
    page.on('request', (request) => {
      sent ||= request.url().endsWith('/api/account/register');
    });

    await page.getByLabel('Email', { exact: true }).fill('nobody-e2e@example.com');
    await page.getByLabel('Summoner name').fill('nobody_e2e');
    await page.getByLabel('Password', { exact: true }).fill('Hextech-Forge-2046!');
    await page.getByLabel('Confirm password').fill('Hextech-Forge-2047!');
    await page.getByLabel('I accept the terms of use').check();
    await page.getByRole('button', { name: 'Forge my account' }).click();

    await expect(page.getByRole('alert')).toBeVisible();
    expect(sent).toBe(false);
  });
});
