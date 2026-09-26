import {
  type HttpInterceptorFn,
  HttpResponse,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { Location } from '@angular/common';
import { provideLocationMocks } from '@angular/common/testing';
import { PLATFORM_ID, REQUEST, RESPONSE_INIT, type Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  type ActivatedRouteSnapshot,
  provideRouter,
  Router,
  withNavigationErrorHandler,
  withRouterConfig,
} from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { routes } from './app.routes';
import { API_BASE_URL } from './core/api/api-base-url';
import type { CatalogMeta } from './core/api/generated/models/catalog-meta';
import { handleNavigationError } from './core/routing/failure/handle-navigation-error';
import { AccountPage } from './features/account/account-page';
import { AdminPage } from './features/admin/admin-page';
import { ApiPortalPage } from './features/api-portal/api-portal-page';
import { EditorPage } from './features/builds/editor/editor-page';
import { SharePage } from './features/builds/share/share-page';
import { TrendsPage } from './features/builds/trends/trends-page';
import { ChampionsPage } from './features/catalogue/champions/champions-page';
import { ItemsPage } from './features/catalogue/items/items-page';
import { RunesPage } from './features/catalogue/runes/runes-page';
import { SummonersPage } from './features/catalogue/summoners/summoners-page';
import { DevelopersPage } from './features/developers/developers-page';
import { DonatePage } from './features/donate/donate-page';
import { EditorialPage } from './features/editorial/editorial-page';
import { ErrorPage } from './features/errors/error-page';
import { HomePage } from './features/home/home-page';
import { ProfilePage } from './features/profile/profile-page';

const LATEST = '16.19.1';
const OLDER = '15.14.1';
const META: CatalogMeta = {
  latest: LATEST,
  versions: [LATEST, OLDER],
  readyVersions: [LATEST],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US', 'fr_FR', 'de_DE', 'ar_AE'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
    { locale: 'de', language: 'de_DE' },
    { locale: 'ar', language: 'ar_AE' },
  ],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};
const DETAILS = /^\/api\/catalog\/[^/]+\/[^/]+\/(?<path>[^/]+\/[^/]+)$/;

// The simulated API: its meta, and any detail at the path it was asked for.
function simulatedApi(requested: string[]): HttpInterceptorFn {
  return (request) => {
    const url = request.urlWithParams;
    requested.push(url);
    const path = DETAILS.exec(url)?.groups?.['path'];
    const body = path ? { canonicalPath: path, profile: { name: path } } : META;
    return of(new HttpResponse({ status: HttpStatusCode.Ok, url, body }));
  };
}

interface Visit {
  readonly router: Router;
  readonly init: ResponseInit;
  readonly requested: string[];
  readonly leaf: ActivatedRouteSnapshot;
}

async function visit(url: string, acceptLanguage = 'en'): Promise<Visit> {
  const init: ResponseInit = { status: HttpStatusCode.Ok, headers: new Headers() };
  const requested: string[] = [];
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        routes,
        withRouterConfig({
          paramsInheritanceStrategy: 'always',
          resolveNavigationPromiseOnError: true,
        }),
        withNavigationErrorHandler(handleNavigationError),
      ),
      provideLocationMocks(),
      provideHttpClient(withInterceptors([simulatedApi(requested)])),
      provideTransloco({
        config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
        loader: class {
          getTranslation = () => of({});
        },
      }),
      { provide: API_BASE_URL, useValue: '' },
      { provide: PLATFORM_ID, useValue: 'server' },
      { provide: RESPONSE_INIT, useValue: init },
      {
        provide: REQUEST,
        useValue: new Request(`http://localhost${url}`, {
          headers: { 'Accept-Language': acceptLanguage },
        }),
      },
    ],
  });
  const router = TestBed.inject(Router);
  // As the first navigation reads the URL, through a Location that drops a trailing slash.
  await router.navigateByUrl(Location.stripTrailingSlash(url));
  let leaf = router.routerState.snapshot.root;
  while (leaf.firstChild) {
    leaf = leaf.firstChild;
  }
  return { router, init, requested, leaf };
}

interface Page {
  readonly url: string;
  readonly page: Type<unknown>;
  readonly heading?: string;
}

const PUBLIC_PAGES: Page[] = [
  { url: '/en', page: HomePage },
  { url: '/fr/', page: HomePage },
  { url: '/en/champions', page: ChampionsPage },
  { url: '/en/items/1036-long-sword', page: ItemsPage },
  { url: `/fr/${OLDER}/runes`, page: RunesPage },
  { url: `/de/${OLDER}/summoners/SummonerFlash`, page: SummonersPage },
  { url: '/en/trends', page: TrendsPage },
  { url: '/en/u/faker', page: ProfilePage },
  { url: '/en/developers', page: DevelopersPage },
  { url: '/en/donate', page: DonatePage },
  { url: '/b/Zx81kQ', page: SharePage },
];

const EDITORIAL_PAGES: Page[] = [
  { url: '/en/about', page: EditorialPage, heading: 'about.index.title' },
  { url: '/fr/about/data', page: EditorialPage, heading: 'about.data.title' },
  { url: '/de/faq', page: EditorialPage, heading: 'about.faq.title' },
  { url: '/ar/changelog', page: EditorialPage, heading: 'changelog.title' },
  { url: '/en/legal/notice', page: EditorialPage, heading: 'legal.notice.title' },
  { url: '/en/legal/privacy', page: EditorialPage, heading: 'legal.privacy.title' },
  { url: '/en/legal/terms', page: EditorialPage, heading: 'legal.terms.title' },
  { url: '/en/legal/cookies', page: EditorialPage, heading: 'legal.cookies.title' },
];

const PRIVATE_PAGES: Page[] = [
  { url: '/en/account/login', page: AccountPage, heading: 'auth.login.title' },
  { url: '/en/account/register', page: AccountPage, heading: 'auth.register.title' },
  { url: '/en/account/verify-email', page: AccountPage, heading: 'nav.account' },
  { url: '/en/account/forgot-password', page: AccountPage, heading: 'auth.reset.request_title' },
  { url: '/en/account/reset-password/t0k3n', page: AccountPage, heading: 'auth.reset.reset_title' },
  { url: '/en/account/profile', page: AccountPage, heading: 'nav.profile' },
  { url: '/en/account/profile/preview', page: AccountPage, heading: 'profile.preview.badge' },
  { url: '/en/account/api', page: ApiPortalPage },
  { url: '/en/account/builds', page: EditorPage, heading: 'build.list.title' },
  { url: '/en/account/builds/new', page: EditorPage, heading: 'build.editor.title_create' },
  { url: '/en/account/builds/42/edit', page: EditorPage, heading: 'build.editor.title_edit' },
  { url: '/en/account/builds/42/import', page: EditorPage, heading: 'build.import.action' },
  { url: '/admin', page: AdminPage },
];

describe('routes (ADR 0005)', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
  });

  it.each([...PUBLIC_PAGES, ...EDITORIAL_PAGES, ...PRIVATE_PAGES])(
    'serves $url with its page',
    async ({ url, page, heading }) => {
      const { router, init, leaf } = await visit(url);

      expect(leaf.component).toBe(page);
      expect(leaf.data['heading']).toBe(heading);
      expect(router.url).toBe(url.replace(/\/$/, ''));
      expect(init.status).toBe(HttpStatusCode.Ok);
    },
  );

  it.each([...EDITORIAL_PAGES, { url: '/en', page: HomePage }])(
    'renders $url without asking the API, so it prerenders without one',
    async ({ url }) => {
      expect((await visit(url)).requested).toEqual([]);
    },
  );

  it('activates the locale of the URL', async () => {
    await visit('/ar/about');

    expect(document.documentElement.lang).toBe('ar');
  });

  it.each([
    '/en/nowhere',
    `/en/${OLDER}`,
    `/en/${OLDER}/trends`,
    '/en/account',
    '/en/account/nowhere',
    '/en/u',
    '/b',
  ])('answers a 404 in place for %s', async (url) => {
    const { router, init, leaf } = await visit(url);

    expect(leaf.component).toBe(ErrorPage);
    expect(leaf.data['outcome']).toEqual({ kind: 'not-found' });
    expect(router.url).toBe(url);
    expect(init.status).toBe(HttpStatusCode.NotFound);
  });

  it('answers a 404 in the default locale for a URL outside the locales', async () => {
    const { leaf, init } = await visit('/xx/champions');

    expect(leaf.component).toBe(ErrorPage);
    expect(init.status).toBe(HttpStatusCode.NotFound);
    expect(document.documentElement.lang).toBe('en');
  });

  it('sends / to the home of the locale the browser prefers', async () => {
    const { router, leaf } = await visit('/', 'pt-BR, fr;q=0.8');

    expect(router.url).toBe('/pt');
    expect(leaf.component).toBe(HomePage);
  });
});
