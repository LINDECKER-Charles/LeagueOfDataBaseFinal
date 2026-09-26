import type { Page } from '@playwright/test';
import { metaOf, olderVersion } from '../../support/catalog';
import { emptyFields, nodesOfType, readHead, typesOf, type Head } from '../../support/head';
import { INDEXED_PAGES, LOCALES, pageUrl } from '../../support/public-pages';
import { expect, test } from '../../support/test';

// The head crawlers read (ADR 0005): the server's, JavaScript off. The head after hydration
// is specs/seo/head.spec.ts's; the diff against production is tools/next/seo-diff's.
const BRAND = 'League Of Data Base';
const INDEXABLE = 'index, follow';
// L3.4: the ItemList describes the first page the server renders, 20 entries at most.
const MAX_LIST_ENTRIES = 20;
const X_DEFAULT = 'x-default';

function pathOf(url: string): string {
  const parsed = new URL(url);
  return `${parsed.pathname}${parsed.search}${parsed.hash}`;
}

// x-default names the page in English; for the home, `/`, which answers by Accept-Language.
function defaultPath(path: string): string {
  return path === '' ? '/' : pageUrl('en', path);
}

// 21 alternates and x-default, each naming the same page in its locale. hreflang is
// case-insensitive: the head writes BCP 47 (`zh-Hant`), the path lowercase (`zh-hant`).
function expectAlternates(head: Head, path: string): void {
  const langs = head.alternates.map((alternate) => alternate.lang.toLowerCase());
  expect([...langs].sort()).toEqual([...LOCALES, X_DEFAULT].sort());
  for (const { lang, href } of head.alternates) {
    const expected = lang === X_DEFAULT ? defaultPath(path) : pageUrl(lang.toLowerCase(), path);
    expect(pathOf(href), lang).toBe(expected);
  }
}

function expectJsonLd(head: Head, types: readonly string[]): void {
  expect(head.jsonLdErrors, 'JSON-LD blocks that do not parse').toBe(0);
  expect(head.jsonLd.length).toBeGreaterThan(0);
  expect([...typesOf(head.jsonLd)]).toEqual(expect.arrayContaining(['WebSite', ...types]));
  expect(emptyFields(head.jsonLd), 'empty JSON-LD fields').toEqual([]);
}

async function crawl(page: Page, url: string): Promise<Head> {
  const response = await page.goto(url);
  expect(response?.status(), url).toBe(200);
  return readHead(page);
}

test.describe('head of the public pages, as crawlers read it', () => {
  test.use({ javaScriptEnabled: false });

  for (const entry of INDEXED_PAGES) {
    const url = pageUrl('en', entry.path);

    test(`gives ${url} its title, canonical, alternates, robots and typed JSON-LD`, async ({
      page,
    }) => {
      const head = await crawl(page, url);

      expect(head.title).toContain(BRAND);
      expect(head.title).not.toContain('patch');
      expect(head.description ?? '').not.toBe('');
      expect(head.description).not.toBe(head.title);
      expect(head.robots).toBe(INDEXABLE);
      expect(head.canonicals.map(pathOf)).toEqual([url]);
      expectAlternates(head, entry.path);
      expectJsonLd(head, entry.types);
    });
  }

  test('keeps a page in its locale: html lang, canonical and alternates', async ({ page }) => {
    const head = await crawl(page, '/fr/champions/Annie');

    await expect(page.locator('html')).toHaveAttribute('lang', 'fr');
    expect(head.canonicals.map(pathOf)).toEqual(['/fr/champions/Annie']);
    expectAlternates(head, 'champions/Annie');
  });

  test('points a regional variant and a later page of a list to the canonical URL', async ({
    page,
  }) => {
    const variant = await crawl(page, '/en/champions/Annie?lang=en_GB');
    expect(variant.canonicals.map(pathOf)).toEqual(['/en/champions/Annie']);

    const later = await crawl(page, '/en/items?page=2');
    expect(later.canonicals.map(pathOf)).toEqual(['/en/items']);
  });

  test('suffixes the title of an archived page with its patch', async ({ page, request }) => {
    const older = olderVersion(await metaOf(request));
    const path = `${older}/champions/Annie`;

    const head = await crawl(page, pageUrl('en', path));

    expect(head.title).toContain(`patch ${older}`);
    expect(head.title).toContain(BRAND);
    expect(head.canonicals.map(pathOf)).toEqual([pageUrl('en', path)]);
    expectAlternates(head, path);
    expectJsonLd(head, ['BreadcrumbList', 'VideoGame', 'Person']);
  });

  test('lists in the ItemList the first entries, each by its canonical URL', async ({ page }) => {
    for (const resource of ['champions', 'items', 'runes', 'summoners']) {
      const head = await crawl(page, pageUrl('en', resource));
      const [list] = nodesOfType(head.jsonLd, 'ItemList');
      const entries = (list?.['itemListElement'] ?? []) as { url?: string }[];

      expect(entries.length, resource).toBeGreaterThan(0);
      expect(entries.length, resource).toBeLessThanOrEqual(MAX_LIST_ENTRIES);
      expect(list?.['numberOfItems'], resource).toBe(entries.length);
      for (const entry of entries) {
        expect(pathOf(entry.url ?? ''), resource).toMatch(new RegExp(`^/en/${resource}/[^/?#]+$`));
      }
    }
  });
});
