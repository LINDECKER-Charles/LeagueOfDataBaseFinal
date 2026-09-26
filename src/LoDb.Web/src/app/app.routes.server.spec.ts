import {
  RenderMode,
  ɵextractRoutesAndCreateRouteTree as extractRoutesAndCreateRouteTree,
} from '@angular/ssr';
import bootstrap from '../main.server';
import { LOCALES } from './core/i18n/locales';
import { keepingGlobals } from './core/testing/keeping-globals';

type Extraction = Awaited<ReturnType<typeof extractRoutesAndCreateRouteTree>>;

const DOCUMENT_HTML =
  '<html><head><base href="/"></head><body><lodb-root></lodb-root></body></html>';
const LATEST = 'public, max-age=0, s-maxage=300, stale-while-revalidate=3600';
const PRIVATE = 'private, no-store';

// What the build does before prerendering, through the private entry point it uses itself:
// app.routes.ts crossed with app.routes.server.ts into the tree the server matches requests
// against. An Angular upgrade that moves it breaks this spec rather than the render modes.
// It renders on the server platform, which overwrites the DOM classes of the worker.
async function extractRoutes(): Promise<Extraction> {
  Reflect.set(globalThis, 'ngServerMode', true);
  try {
    return await keepingGlobals(() =>
      extractRoutesAndCreateRouteTree({
        url: new URL('http://localhost/'),
        manifest: {
          baseHref: '/',
          bootstrap: async () => bootstrap,
          assets: {
            'index.server.html': {
              size: DOCUMENT_HTML.length,
              hash: 'spec',
              text: async () => DOCUMENT_HTML,
            },
          },
        },
        invokeGetPrerenderParams: true,
      }),
    );
  } finally {
    Reflect.set(globalThis, 'ngServerMode', undefined);
  }
}

interface Case {
  readonly url: string;
  readonly mode: RenderMode;
  readonly headers: Record<string, string>;
}

const EDITORIAL = [
  'about',
  'about/data',
  'faq',
  'changelog',
  'legal/notice',
  'legal/privacy',
  'legal/terms',
  'legal/cookies',
];
const PUBLIC = { 'Cache-Control': LATEST };
const HIDDEN = { 'Cache-Control': PRIVATE, 'X-Robots-Tag': 'noindex' };

const CASES: Case[] = [
  { url: '/en/about', mode: RenderMode.Prerender, headers: PUBLIC },
  { url: '/fr/about/data', mode: RenderMode.Prerender, headers: PUBLIC },
  { url: '/zh-hant/faq', mode: RenderMode.Prerender, headers: PUBLIC },
  { url: '/ar/changelog', mode: RenderMode.Prerender, headers: PUBLIC },
  { url: '/pt/legal/cookies', mode: RenderMode.Prerender, headers: PUBLIC },
  // Not prerendered: rendered per request, where the locale guard answers its 404.
  { url: '/xx/about', mode: RenderMode.Server, headers: PUBLIC },
  { url: '/en', mode: RenderMode.Server, headers: PUBLIC },
  { url: '/en/champions', mode: RenderMode.Server, headers: PUBLIC },
  { url: '/en/items/1036-long-sword', mode: RenderMode.Server, headers: PUBLIC },
  { url: '/en/15.14.1/runes/8100-domination', mode: RenderMode.Server, headers: PUBLIC },
  { url: '/en/trends', mode: RenderMode.Server, headers: PUBLIC },
  { url: '/en/u/faker', mode: RenderMode.Server, headers: PUBLIC },
  { url: '/en/developers', mode: RenderMode.Server, headers: PUBLIC },
  { url: '/en/donate', mode: RenderMode.Server, headers: PUBLIC },
  { url: '/en/15.14.1', mode: RenderMode.Server, headers: PUBLIC },
  { url: '/en/nowhere/at/all', mode: RenderMode.Server, headers: PUBLIC },
  { url: '/b/Zx81kQ', mode: RenderMode.Server, headers: { ...PUBLIC, 'X-Robots-Tag': 'noindex' } },
  { url: '/b', mode: RenderMode.Server, headers: PUBLIC },
  // Rendered in the browser, which shows the 404 of a missing private page with a 200.
  { url: '/en/account', mode: RenderMode.Client, headers: HIDDEN },
  { url: '/en/account/login', mode: RenderMode.Client, headers: HIDDEN },
  { url: '/en/account/reset-password/t0k3n', mode: RenderMode.Client, headers: HIDDEN },
  { url: '/en/account/profile/preview', mode: RenderMode.Client, headers: HIDDEN },
  { url: '/en/account/api', mode: RenderMode.Client, headers: HIDDEN },
  { url: '/en/account/builds/42/edit', mode: RenderMode.Client, headers: HIDDEN },
  { url: '/admin', mode: RenderMode.Client, headers: HIDDEN },
];

describe('serverRoutes (ADR 0005)', () => {
  let extraction: Extraction;

  // The render reads the base href through the global DOM adapter, the jsdom one under
  // Vitest: give it the `<base href="/">` of src/index.html.
  beforeAll(async () => {
    const base = document.createElement('base');
    base.href = '/';
    document.head.prepend(base);
    try {
      extraction = await extractRoutes();
    } finally {
      base.remove();
    }
  });

  it('matches every server route to a page, and every page to a server route', () => {
    expect(extraction.errors).toEqual([]);
  });

  it.each(CASES)('renders $url in its mode, with its headers', ({ url, mode, headers }) => {
    const route = extraction.routeTree.match(url);

    expect(route?.renderMode).toBe(mode);
    expect(route?.headers).toEqual(headers);
  });

  it('prerenders the editorial pages, and only them, in every locale', () => {
    const prerendered = extraction.routeTree
      .toObject()
      .filter((route) => route.renderMode === RenderMode.Prerender)
      .map((route) => route.route);

    const expected = LOCALES.flatMap((locale) => EDITORIAL.map((path) => `/${locale}/${path}`));
    expect(prerendered.sort()).toEqual(expected.sort());
  });

  it('leaves jsdom its DOM classes for the specs that run next in this worker', () => {
    const element = document.createElement('p');
    const listener = vi.fn();
    element.addEventListener('ping', listener);
    element.dispatchEvent(new Event('ping'));

    expect(listener).toHaveBeenCalledOnce();
    expect(element).toBeInstanceOf(Node);
  });
});
