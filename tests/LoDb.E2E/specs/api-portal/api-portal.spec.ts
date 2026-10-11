import { createMember, signInMember } from '../../support/member-account';
import { expect, test } from '../../support/test';
import { discardAccount, type TestAccount } from '../account/accounts';
import { PORTAL, portalToast, shownSecret, usageStatus } from './portal';

const OK = 200;
const FORBIDDEN = 403;

// The account of the journey, deleted even when a step fails: no run leaves one behind. It is
// created by the CLI: the registration form's quota is account/register.spec.ts's.
let created: TestAccount | undefined;

test.afterEach(async ({ playwright, baseURL }) => {
  if (created) {
    await discardAccount(playwright.request, baseURL, created);
    created = undefined;
  }
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
    await expect(portalToast(page, 'API key created.')).toBeVisible();
    await expect(page.getByTestId('api-key-prefix')).toHaveText(`${secret.slice(0, 12)}…`);
    await expect(page.getByTestId('api-key-plan')).toHaveText('Free');
    expect(await usageStatus(request, secret)).toBe(OK);
  });

  await test.step('never shows the secret again', async () => {
    await page.reload();
    await expect(page.getByTestId('api-key-prefix')).toBeVisible();
    await expect(page.getByTestId('api-key-secret')).toHaveCount(0);
  });

  await test.step('regenerates it: the old secret is refused, the new one served', async () => {
    await page.getByRole('button', { name: 'Regenerate the key' }).click();
    await expect(
      portalToast(page, 'Key regenerated — the previous secret no longer works.'),
    ).toBeVisible();
    const renewed = await shownSecret(page);
    expect(renewed).not.toBe(secret);
    expect(await usageStatus(request, secret)).toBe(FORBIDDEN);
    expect(await usageStatus(request, renewed)).toBe(OK);
    secret = renewed;
  });

  await test.step('revokes it: /v1 refuses it from the next request', async () => {
    await page.getByRole('button', { name: 'Revoke', exact: true }).click();
    await expect(portalToast(page, 'Key revoked.')).toBeVisible();
    await expect(page.getByTestId('api-key-secret')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Create my key' })).toBeVisible();
    expect(await usageStatus(request, secret)).toBe(FORBIDDEN);
  });

  expect(consoleErrors).toEqual([]);
});
