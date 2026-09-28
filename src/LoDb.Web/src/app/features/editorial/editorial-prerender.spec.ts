import { provideHttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, mergeApplicationConfig } from '@angular/core';
import {
  type BootstrapContext,
  bootstrapApplication,
  provideClientHydration,
} from '@angular/platform-browser';
import { provideServerRendering, renderApplication } from '@angular/platform-server';
import { provideRouter, RouterOutlet } from '@angular/router';
import { activateLocale } from '../../core/i18n/activate-locale';
import { LOCALES } from '../../core/i18n/locales';
import { provideI18n } from '../../core/i18n/provide-i18n';
import { CANONICAL_ORIGIN } from '../../core/seo/canonical-origin';
import { keepingGlobals } from '../../core/testing/keeping-globals';
import { EDITORIAL_ROUTES } from './editorial.routes';

const DOCUMENT_HTML =
  '<html><head><base href="/"></head><body><lodb-root></lodb-root></body></html>';
const PAGES = [
  'about',
  'about/data',
  'faq',
  'changelog',
  'legal/notice',
  'legal/privacy',
  'legal/terms',
  'legal/cookies',
];
// The build's static files, keyed by path; the catalogues are left empty, their keys shown.
const STATIC: Record<string, unknown> = {
  '/changelog/manifest.json': { patches: [{ id: '2026-08-22-tamis', version: '2.2.1' }] },
  '/changelog/2026-08-22-tamis.json': {
    version: '2.2.1',
    codename: 'Tamis',
    date: '2026-08-22',
    type: 'patch',
    summary: 'Hydrated pages.',
  },
};

@Component({
  selector: 'lodb-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Root {}

// What the prerender serves: the build's files. Any other request, the API's, is recorded.
function stubStaticFetch(): string[] {
  const outside: string[] = [];
  vi.stubGlobal('fetch', (input: RequestInfo | URL) => {
    const url = new URL(input instanceof Request ? input.url : String(input), 'http://ssr.test');
    if (url.pathname.startsWith('/i18n/')) {
      return Promise.resolve(Response.json({}));
    }
    const file = STATIC[url.pathname];
    if (file === undefined) {
      outside.push(url.href);
      return Promise.resolve(new Response('Not Found', { status: 404 }));
    }
    return Promise.resolve(Response.json(file));
  });
  return outside;
}

// The editorial routes below the locale, as app.routes.ts mounts them, on the server platform.
async function prerender(url: string): Promise<string> {
  Reflect.set(globalThis, 'ngServerMode', true);
  try {
    const config = mergeApplicationConfig(
      {
        providers: [
          provideRouter([
            { path: ':locale', resolve: { locale: activateLocale }, children: EDITORIAL_ROUTES },
          ]),
          provideHttpClient(),
          provideClientHydration(),
          provideI18n(),
          { provide: CANONICAL_ORIGIN, useValue: 'https://league-of-data-base.com' },
        ],
      },
      { providers: [provideServerRendering()] },
    );
    const bootstrap = (context: BootstrapContext) => bootstrapApplication(Root, config, context);
    return await keepingGlobals(() =>
      renderApplication(bootstrap, { document: DOCUMENT_HTML, url }),
    );
  } finally {
    Reflect.set(globalThis, 'ngServerMode', undefined);
  }
}

// Each locale renders one page, in turn, so every page renders under several locales.
const CASES = LOCALES.map((locale, index) => ({ locale, page: PAGES[index % PAGES.length] }));

describe('the editorial prerender', () => {
  let base: HTMLBaseElement;

  // Relative URLs resolve against the base href read through the global DOM adapter, the
  // jsdom one under Vitest: give it the `<base href="/">` of src/index.html.
  beforeEach(() => {
    base = document.createElement('base');
    base.href = '/';
    document.head.prepend(base);
  });

  afterEach(() => {
    base.remove();
    vi.unstubAllGlobals();
  });

  it.each(CASES)('renders /$locale/$page from the build files alone', async ({ locale, page }) => {
    const outside = stubStaticFetch();

    const html = await prerender(`/${locale}/${page}`);

    expect(outside).toEqual([]);
    expect(html).toContain(`<html lang="${locale}"`);
    expect(html).toMatch(/<h1[^>]*>/);
    expect(html).toContain(
      `rel="canonical" href="https://league-of-data-base.com/${locale}/${page}"`,
    );
  });

  it('renders the published releases into the changelog', async () => {
    stubStaticFetch();

    const html = await prerender('/en/changelog');

    expect(html).toContain('id="v2-2-1"');
    expect(html).toContain('Tamis');
    expect(html).toContain('Hydrated pages.');
  });

  it('renders the inventory placeholders, the counters being left to the browser', async () => {
    const outside = stubStaticFetch();

    const html = await prerender('/fr/about/data');

    expect(outside).toEqual([]);
    expect(html).toContain('—');
    expect(html).toContain('"@type":"Dataset"');
  });
});
