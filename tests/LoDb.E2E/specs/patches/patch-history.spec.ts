import type { Locator, Page } from '@playwright/test';
import { expectNoAccessibilityViolations } from '../../support/accessibility';
import { metaOf, olderVersion } from '../../support/catalog';
import { readHead, typesOf } from '../../support/head';
import { expect, test } from '../../support/test';

const PAGE = '/en/patches';
const LISTS = ['champions', 'items', 'runes', 'summoners'];
// What a link of the history answers: its page, never a redirect nor a 404. A past patch not
// stored yet may answer the 503 of a cold version, shed on purpose while it is ingested.
const OK = 200;
const PINNED_ANSWERS = [OK, 503];

// The links of a version's row, found by the version its header names.
function linksOf(page: Page, version: string): Locator {
  const header = page.locator('th', {
    hasText: new RegExp(`^\\s*${version.replace(/\./g, '\\.')}(?:\\s|$)`),
  });
  return page.locator('tbody tr').filter({ has: header }).locator('a');
}

async function hrefsOf(links: Locator): Promise<(string | null)[]> {
  return links.evaluateAll((anchors) => anchors.map((anchor) => anchor.getAttribute('href')));
}

test.describe('patch history as crawlers read it', { tag: '@readonly' }, () => {
  test.use({ javaScriptEnabled: false });

  test('lists every version Data Dragon lists, each linking its four lists', async ({
    page,
    request,
  }) => {
    const meta = await metaOf(request);
    const older = olderVersion(meta);

    const response = await page.goto(PAGE);

    expect(response?.status()).toBe(200);
    await expect(page.getByRole('heading', { level: 1, name: 'Patch history' })).toBeVisible();
    await expect(page.locator('tbody th')).toHaveCount(meta.versions.length);
    expect(await hrefsOf(linksOf(page, meta.latest))).toEqual(LISTS.map((list) => `/en/${list}`));
    expect(await hrefsOf(linksOf(page, older))).toEqual(
      LISTS.map((list) => `/en/${older}/${list}`),
    );
  });

  test('links pages that answer themselves, never a redirect nor a 404', async ({
    page,
    request,
  }) => {
    const meta = await metaOf(request);
    const older = olderVersion(meta);
    await page.goto(PAGE);
    const latest = await hrefsOf(linksOf(page, meta.latest));
    const [pinned] = await hrefsOf(linksOf(page, older).filter({ hasText: 'Runes' }));

    for (const href of latest) {
      const answer = await request.get(href ?? '', { maxRedirects: 0 });
      expect(answer.status(), href ?? '').toBe(OK);
    }
    const answer = await request.get(pinned ?? '', { maxRedirects: 0 });
    expect(PINNED_ANSWERS, pinned ?? '').toContain(answer.status());
  });

  test('is an indexable page with its canonical and its breadcrumb', async ({ page }) => {
    await page.goto(PAGE);
    const head = await readHead(page);

    expect(head.robots).toBe('index, follow');
    expect(head.canonicals.map((url) => new URL(url).pathname)).toEqual([PAGE]);
    expect(typesOf(head.jsonLd).has('BreadcrumbList')).toBe(true);
  });

  test('is linked from the footer of every page', async ({ page }) => {
    await page.goto('/fr/champions');

    await expect(page.locator('footer a[href="/fr/patches"]')).toHaveText('Historique des patchs');
  });
});

test(
  'opens a past patch from the history in the browser',
  { tag: '@readonly' },
  async ({ page, request, consoleErrors }) => {
    const older = olderVersion(await metaOf(request));
    await page.goto(PAGE);
    await page.waitForLoadState('networkidle');
    await expectNoAccessibilityViolations(page);

    await linksOf(page, older).filter({ hasText: 'Runes' }).click();

    await expect(page).toHaveURL((url) => url.pathname === `/en/${older}/runes`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    expect(consoleErrors).toEqual([]);
  },
);
