import type { APIRequestContext, Page } from '@playwright/test';
import { metaOf } from '../../support/catalog';
import { emptyFields, readHead, typesOf, type Head } from '../../support/head';
import { LOCALES, pageUrl } from '../../support/public-pages';
import {
  findOpenerPage,
  olderPatchesWithChanges,
  statePageOf,
  type EntityState,
  type StatePage,
} from '../../support/states';
import { expect, test } from '../../support/test';

// The head crawlers read (ADR 0005): the server's, JavaScript off. Each page's head is its own
// unit tests'; two samples prove the integrated head on real data and configuration, rendered
// on request and prerendered. A past patch's page is indexed once per state of its entity
// (amendment of 2026-10-22): the API decides from its index, the SSR server writes the head,
// and only the stack shows both agree, on the entities its index puts in each state. The head
// after hydration is specs/public/smoke.spec.ts's; the diff against production is
// tools/seo-diff's.
const BRAND = 'League Of Data Base';
const INDEXABLE = 'index, follow';
const LANGUAGE = 'en_US';
const NOT_INDEXED = 'noindex, follow';
// How the description of an opener begins (public/i18n/seo/en.json, state.summary*).
const SUMMARY: Readonly<Record<Exclude<EntityState['kind'], 'baseline'>, string>> = {
  changed: 'Changed in patch',
  new: 'New in patch',
  returned: 'Back in patch',
};
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

// The opener of a closed state of several patches, whose patch has a page: its first version
// is indexed, the others point to it.
function isClosedRun(state: EntityState): boolean {
  const isRun = state.lastVersion !== state.openerVersion;
  return !state.isOpen && state.isOpener && isRun && state.kind !== 'baseline';
}

async function closedRunOpener(request: APIRequestContext): Promise<StatePage> {
  const meta = await metaOf(request);
  return findOpenerPage(request, meta, 'closed state of several patches', isClosedRun);
}

function summaryOf(state: EntityState): string {
  return state.kind === 'baseline' ? '' : `${SUMMARY[state.kind]} ${state.openerVersion}`;
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

  test('indexes the opener of a closed state at its own URL, in every locale', async ({
    page,
    request,
  }) => {
    const opener = await closedRunOpener(request);
    const path = `${opener.version}/${opener.path}`;

    const head = await crawl(page, pageUrl('en', path));

    const { openerVersion, lastVersion } = opener.state;
    expect(head.title).toContain(`(patches ${openerVersion} to ${lastVersion})`);
    const summary = summaryOf(opener.state);
    expect(head.description?.slice(0, summary.length)).toBe(summary);
    expect(head.robots).toBe(INDEXABLE);
    expect(head.canonicals.map(pathOf)).toEqual([pageUrl('en', path)]);
    expectAlternates(head, path);
    expectJsonLd(head, ['BreadcrumbList']);
  });

  test("points another patch of a closed state to the state's opener", async ({
    page,
    request,
  }) => {
    const opener = await closedRunOpener(request);
    const last = await statePageOf(request, opener.state.lastVersion ?? '', opener);
    expect(last?.state.isOpener, 'the last patch of the state').toBe(false);

    const head = await crawl(page, pageUrl('en', `${last?.version}/${last?.path}`));

    expect(head.title).toContain(`(patch ${last?.version})`);
    expect(head.robots).toBe(INDEXABLE);
    expect(head.canonicals.map(pathOf)).toEqual([
      pageUrl('en', `${opener.version}/${opener.path}`),
    ]);
    expect(head.alternates).toEqual([]);
    expect(head.jsonLd).toEqual([]);
  });

  // A revisable page: its state still holds at the latest patch, whose short URL is indexed.
  test('points a past patch of the open state to the short URL', async ({ page, request }) => {
    const meta = await metaOf(request);
    const isOpen = (state: EntityState) => state.isOpen;
    const open = await findOpenerPage(request, meta, 'open state older than the latest', isOpen);

    const head = await crawl(page, pageUrl('en', `${open.version}/${open.path}`));

    expect(head.title).toContain(`(patch ${open.version})`);
    expect(head.robots).toBe(INDEXABLE);
    expect(head.canonicals.map(pathOf)).toEqual([pageUrl('en', open.path)]);
    expect(head.alternates).toEqual([]);
    expect(head.jsonLd).toEqual([]);
  });

  test("indexes the page of a past patch's changes at its own URL", async ({ page, request }) => {
    const [version] = olderPatchesWithChanges(await metaOf(request));
    expect(version, 'the index of states must cover the patches before the latest').toBeDefined();
    const answer = await request.get(`/api/catalog/${version}/${LANGUAGE}/changes`);
    const { hasChanges } = (await answer.json()) as { readonly hasChanges: boolean };
    const path = `${version}/changes`;

    const head = await crawl(page, pageUrl('en', path));

    expect(head.title).toContain(`Patch ${version}`);
    expect(head.canonicals.map(pathOf)).toEqual([pageUrl('en', path)]);
    if (hasChanges) {
      expect(head.robots).toBe(INDEXABLE);
      expectAlternates(head, path);
      expectJsonLd(head, ['BreadcrumbList', 'ItemList']);
    } else {
      expect(head.robots).toBe(NOT_INDEXED);
      expect(head.alternates).toEqual([]);
    }
  });
});
