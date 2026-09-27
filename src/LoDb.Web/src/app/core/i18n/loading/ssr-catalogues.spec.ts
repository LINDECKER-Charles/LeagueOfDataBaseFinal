import { provideHttpClient } from '@angular/common/http';
import {
  APP_ID,
  type ApplicationConfig,
  ChangeDetectionStrategy,
  Component,
  mergeApplicationConfig,
  type Type,
  ɵgetDocument as getDocument,
} from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  type BootstrapContext,
  bootstrapApplication,
  Meta,
  provideClientHydration,
} from '@angular/platform-browser';
import { provideServerRendering, renderApplication } from '@angular/platform-server';
import { provideRouter, RouterOutlet } from '@angular/router';
import { type Translation, TranslocoPipe, provideTranslocoScope } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { Shell } from '../../layout/shell/shell';
import { keepingGlobals } from '../../testing/keeping-globals';
import { activateLocale } from '../activate-locale';
import { provideI18n } from '../provide-i18n';
import { TranslocoHttpLoader } from './transloco-http-loader';

// Served by the stubbed fetch, keyed by path: the render resolves the loader's relative URL.
const CATALOGUES: Record<string, Translation> = {
  '/i18n/en.json': { base: { title: 'Hello', tagline: 'Only in English' } },
  '/i18n/fr.json': { base: { title: 'Bonjour' } },
  '/i18n/about/en.json': {
    index: { title: 'About' },
    data: { title: 'Our data' },
    faq: { title: 'FAQ' },
  },
  '/i18n/about/fr.json': {
    index: { title: 'À propos' },
    data: { title: 'Nos données' },
    faq: { title: 'Foire aux questions' },
  },
  '/i18n/api/en.json': { nav: { developers: 'Developers' } },
  '/i18n/api/fr.json': { nav: { developers: 'Développeurs' } },
};
// Angular's default, which the render keeps and TestBed does not.
const SERVER_APP_ID = 'ng';
// The chrome's scope lands at once, the route matches later, the root catalogue last.
const MATCH_DELAY_MS = 20;
const ROOT_DELAY_MS = 40;
const DOCUMENT_HTML =
  '<html><head><base href="/"></head><body><lodb-root></lodb-root></body></html>';

@Component({
  selector: 'lodb-title-page',
  imports: [TranslocoPipe],
  template: `<h1>{{ 'base.title' | transloco }}</h1>
    <p>{{ 'base.tagline' | transloco }}</p>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class TitlePage {}

@Component({
  selector: 'lodb-chrome-page',
  imports: [Shell],
  template: '<lodb-shell><p>Page</p></lodb-shell>',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class ChromePage {}

@Component({
  selector: 'lodb-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Root {}

@Component({
  selector: 'lodb-scoped-label',
  imports: [TranslocoPipe],
  template: `<b>{{ 'base.title' | transloco }}</b>`,
  providers: [provideTranslocoScope('api')],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class ScopedLabel {}

// Like app.html: the chrome sits outside the outlet, so it renders before the navigation ends.
@Component({
  selector: 'lodb-root',
  imports: [RouterOutlet, ScopedLabel],
  template: '<lodb-scoped-label /><router-outlet />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class ChromeRoot {}

const after = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

// `delays`: milliseconds before a path answers, none by default.
function stubCatalogueFetch(delays: Record<string, number> = {}): ReturnType<typeof vi.fn> {
  const fetch = vi.fn(async (input: RequestInfo | URL) => {
    const url = new URL(input instanceof Request ? input.url : String(input), 'http://ssr.test');
    const catalogue = CATALOGUES[url.pathname];
    await after(delays[url.pathname] ?? 0);
    return catalogue ? Response.json(catalogue) : new Response('Not Found', { status: 404 });
  });
  vi.stubGlobal('fetch', fetch);
  return fetch;
}

// A function, not a constant: provideClientHydration() picks its browser or server flavour
// when called, from the `ngServerMode` flag the server bundle defines at build time.
function browserConfig(): ApplicationConfig {
  return {
    providers: [
      provideRouter([
        { path: ':locale', resolve: { locale: activateLocale }, component: TitlePage },
        { path: ':locale/chrome', resolve: { locale: activateLocale }, component: ChromePage },
        {
          path: ':locale/late',
          canMatch: [() => after(MATCH_DELAY_MS).then(() => true)],
          resolve: { locale: activateLocale },
          component: TitlePage,
        },
      ]),
      provideHttpClient(),
      provideClientHydration(),
      provideI18n(),
      // The chrome's theme sets a meta tag. Meta builds it through the global DOM adapter,
      // which Vitest pins to jsdom's, then inserts it in the render's domino document: the
      // two do not mix, and the tag is not what these specs check.
      { provide: Meta, useValue: { updateTag: () => null } },
    ],
  };
}

// Merged in the order of app.config.server.ts, which matters: the transfer cache must key a
// request before the server's own root interceptor makes its URL absolute, or the browser,
// which requests the relative URL, would never find it.
async function renderOnServer(url: string, root: Type<unknown> = Root): Promise<string> {
  Reflect.set(globalThis, 'ngServerMode', true);
  try {
    const serverConfig = mergeApplicationConfig(browserConfig(), {
      providers: [provideServerRendering()],
    });
    const bootstrap = (context: BootstrapContext) =>
      bootstrapApplication(root, serverConfig, context);
    return await keepingGlobals(() =>
      renderApplication(bootstrap, { document: DOCUMENT_HTML, url }),
    );
  } finally {
    // The browser app of the next test runs in this same process, unlike a real server.
    Reflect.set(globalThis, 'ngServerMode', undefined);
  }
}

function bodyOf(html: string): string {
  return /<body>([\s\S]*)<\/body>/.exec(html)?.[1] ?? '';
}

describe('i18n catalogues in SSR', () => {
  // The render resolves relative URLs against the base href it reads through the global DOM
  // adapter. Under Vitest that is the browser one, which reads this jsdom document rather
  // than the rendered one: give it the `<base href="/">` of src/index.html.
  let base: HTMLBaseElement;

  beforeEach(() => {
    base = document.createElement('base');
    base.href = '/';
    document.head.prepend(base);
  });

  afterEach(() => {
    base.remove();
    document.body.innerHTML = '';
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('renders the page already translated, with the en fallback for missing keys', async () => {
    const fetch = stubCatalogueFetch();

    const html = await renderOnServer('/fr/');

    expect(html).toContain('<html lang="fr">');
    expect(html).toMatch(/<h1>Bonjour<\/h1>\s*<p>Only in English<\/p>/);
    const fetched = fetch.mock.calls.map(([input]) => new URL(String(input)).pathname);
    expect(fetched.sort()).toEqual(['/i18n/en.json', '/i18n/fr.json']);
  });

  it('renders the header and footer with the labels of their catalogue scopes', async () => {
    stubCatalogueFetch();
    // The stubbed root catalogues lack the chrome's own labels, which is not the point here.
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);

    const body = bodyOf(await renderOnServer('/fr/chrome'));

    expect(body).toContain('Développeurs');
    expect(body).toContain('À propos');
    expect(body).toContain('Nos données');
    expect(body).toContain('Foire aux questions');
    expect(body.match(/>\s*(?:about|api)\.[a-z_.]+\s*</g)).toBeNull();
  });

  it('renders the chrome in the root catalogue, even when one of its scopes lands first', async () => {
    stubCatalogueFetch({ '/i18n/en.json': ROOT_DELAY_MS });

    const body = bodyOf(await renderOnServer('/en/late', ChromeRoot));

    expect(body).toContain('<b>Hello</b>');
    expect(body).toContain('<h1>Hello</h1>');
  });

  it('leaves jsdom its DOM classes and document for the specs that run next in this worker', async () => {
    stubCatalogueFetch();
    await renderOnServer('/fr/');

    const element = document.createElement('p');
    const listener = vi.fn();
    element.addEventListener('ping', listener);
    element.dispatchEvent(new Event('ping'));

    expect(listener).toHaveBeenCalledOnce();
    expect(element).toBeInstanceOf(Node);
    expect(getDocument()).toBe(document);
  });

  it('embeds the catalogues in the page, so the browser does not download them again', async () => {
    stubCatalogueFetch();
    const body = bodyOf(await renderOnServer('/fr/'));
    expect(body).toContain('<script id="ng-state" type="application/json">');

    // The browser starts on the rendered page: TransferState reads it once, at startup.
    document.body.innerHTML = body;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideClientHydration(),
        { provide: APP_ID, useValue: SERVER_APP_ID },
      ],
    });
    const browserFetch = stubCatalogueFetch();

    const catalogue = await firstValueFrom(
      TestBed.inject(TranslocoHttpLoader).getTranslation('fr'),
    );

    expect(catalogue).toEqual(CATALOGUES['/i18n/fr.json']);
    expect(browserFetch).not.toHaveBeenCalled();
  });
});
