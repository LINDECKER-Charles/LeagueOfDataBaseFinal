import type { Page } from '@playwright/test';
import { emptyFields, readHead, typesOf, type Head } from '../../support/head';
import { LOCALES, pageUrl } from '../../support/public-pages';
import { expect, test } from '../../support/test';

// The head crawlers read (ADR 0005): the server's, JavaScript off. Each page's head is its own
// unit tests'; two samples prove the integrated head on real data and configuration, rendered
// on request and prerendered. The head after hydration is specs/public/smoke.spec.ts's; the
// diff against production is tools/seo-diff's.
const BRAND = 'League Of Data Base';
const INDEXABLE = 'index, follow';
const X_DEFAULT = 'x-default';

interface SampledPage {
  /** Path below `/en/`. */
  readonly path: string;
  /** JSON-LD types the page carries, besides the graph of the site. */
  readonly types: readonly string[];
}

const SAMPLED_PAGES: readonly SampledPage[] = [
  { path: 'champions/Annie', types: ['BreadcrumbList', 'VideoGame', 'Person'] },
  { path: 'about', types: ['AboutPage'] },
];

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

test.describe('head of the public pages, as crawlers read it', { tag: '@readonly' }, () => {
  test.use({ javaScriptEnabled: false });

  for (const entry of SAMPLED_PAGES) {
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
});
