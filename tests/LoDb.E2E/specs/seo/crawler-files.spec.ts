import type { APIRequestContext } from '@playwright/test';
import { readHead } from '../../support/head';
import { expect, test } from '../../support/test';

const OK = 200;
// The English file of a past patch in the sitemap index: /sitemaps/en/16.20.1.xml.
const PATCH_FILE = /^\/sitemaps\/en\/\d+\.\d+\.\d+\.xml$/;
// The page of a patch's changes, which a patch's file lists first.
const CHANGES_PAGE = /\/changes$/;
// Files of past patches read before giving up: the newest ones may list their page of changes
// alone, while their entities' states are still open.
const PATCH_FILES_READ = 5;

function locationsOf(xml: string): string[] {
  return [...xml.matchAll(/<loc>([^<]+)<\/loc>/g)].map((match) => new URL(match[1] ?? '').pathname);
}

async function locationsAt(request: APIRequestContext, path: string): Promise<string[]> {
  const file = await request.get(path);
  expect(file.status(), path).toBe(OK);
  expect(file.headers()['content-type']).toBe('application/xml; charset=utf-8');
  return locationsOf(await file.text());
}

// The first entity page a past patch's file lists: the opener of a closed state.
async function firstOpener(request: APIRequestContext, files: readonly string[]) {
  for (const file of files.filter((path) => PATCH_FILE.test(path)).slice(0, PATCH_FILES_READ)) {
    const opener = (await locationsAt(request, file)).find((path) => !CHANGES_PAGE.test(path));
    if (opener !== undefined) {
      return opener;
    }
  }
  throw new Error(
    'No file of a past patch lists the opener of a closed state: index the patches before the' +
      ' latest first (tools/states/index-past-patches.sh)',
  );
}

// Files nginx must hand to the API (server.d/seo.conf): the SSR server knows none of them.
// Their content is the API tests'; here, that each one reaches the API through nginx, and that
// a past patch's page the sitemaps announce is indexed at that URL: the sitemaps read the index
// of states in the API, the head comes from the SSR server, and only the stack joins them
// (docs/produit/seo-une-page-par-etat.md, § 3.3 and § 3.6).
test.describe('crawler files', { tag: '@readonly' }, () => {
  // The head a crawler reads: the server's.
  test.use({ javaScriptEnabled: false });

  test('serves robots.txt, llms.txt and the sitemap index from the API', async ({
    page,
    request,
  }) => {
    const robots = await request.get('/robots.txt');

    expect(robots.status()).toBe(OK);
    expect(robots.headers()['content-type']).toBe('text/plain; charset=utf-8');

    await test.step('serves llms.txt from the API', async () => {
      const llms = await request.get('/llms.txt');

      expect(llms.status()).toBe(OK);
      expect(llms.headers()['content-type']).toBe('text/markdown; charset=utf-8');
    });

    // 503 only while the API has not loaded its index of states, which the stack builds
    // before the API starts (service `states`).
    const files = await test.step('serves the sitemap index from the API', () =>
      locationsAt(request, '/sitemap.xml'));

    await test.step('announces the opener of a closed state at its canonical URL', async () => {
      const opener = await firstOpener(request, files);
      const response = await page.goto(opener);

      expect(response?.status(), opener).toBe(OK);
      const head = await readHead(page);
      expect(head.canonicals.map((href) => new URL(href).pathname)).toEqual([opener]);
    });
  });
});
