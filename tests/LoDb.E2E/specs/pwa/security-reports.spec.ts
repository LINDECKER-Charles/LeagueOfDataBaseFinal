import { expect, test } from '../../support/test';

const REPORTS = '/api/csp-reports';
const LEGACY_REPORT = {
  'csp-report': { 'effective-directive': 'script-src-elem', disposition: 'report' },
};

// Not @readonly: the read-only suite may run against the production site (tools/cutover), where
// a report taken in feeds the monitoring that tells when the pages' policies can be tightened.
test.describe('the reports of the pages policies', () => {
  test("are taken in from the site's own pages", async ({ request, baseURL }) => {
    const origin = new URL(baseURL ?? '').origin;

    const legacy = await request.post(REPORTS, {
      headers: { Origin: origin, 'Content-Type': 'application/csp-report' },
      data: JSON.stringify(LEGACY_REPORT),
    });
    const batch = await request.post(REPORTS, {
      headers: { Origin: origin, 'Content-Type': 'application/reports+json' },
      data: JSON.stringify([
        { type: 'csp-violation', body: { effectiveDirective: 'img-src', disposition: 'enforce' } },
      ]),
    });

    expect(legacy.status()).toBe(204);
    expect(batch.status()).toBe(204);
  });
});

// Refused before it is read: nothing is counted, so it may run anywhere.
test.describe('the reports of another site', { tag: '@readonly' }, () => {
  test('are refused', async ({ request }) => {
    const forged = await request.post(REPORTS, {
      headers: { Origin: 'https://evil.example', 'Content-Type': 'application/csp-report' },
      data: JSON.stringify(LEGACY_REPORT),
    });

    expect(forged.status()).toBe(403);
  });
});

// The policies isolate the pages from the windows that open them: signing in with Google must
// remain a navigation of the page, which leaves for the API and comes back to the site.
test('starts the Google sign-in as a navigation of the page', async ({ page }) => {
  await page.goto('/en/account/login');

  await page.getByRole('button', { name: 'Continue with Google' }).click();

  // The local stack has no Google client: the API sends the page back with the reason.
  await expect(page).toHaveURL(/\/en\/account\/login\?error=google-unavailable/);
  await expect(page.getByText('Google sign-in is not set up on this server.')).toBeVisible();
});
