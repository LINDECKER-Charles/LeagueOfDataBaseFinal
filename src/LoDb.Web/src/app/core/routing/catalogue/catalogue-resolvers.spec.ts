import {
  HttpErrorResponse,
  HttpHeaders,
  type HttpInterceptorFn,
  HttpResponse,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { provideLocationMocks } from '@angular/common/testing';
import { Component, PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  type ActivatedRouteSnapshot,
  provideRouter,
  Router,
  type Routes,
  withNavigationErrorHandler,
  withRouterConfig,
} from '@angular/router';
import { of, throwError } from 'rxjs';
import { API_BASE_URL } from '../../api/api-base-url';
import type { EntityState } from '../../api/generated/models/entity-state';
import { localeGuard } from '../../i18n/locale-guard';
import { catalogMetaOf } from '../../testing/catalog-meta-of';
import { handleNavigationError } from '../failure/handle-navigation-error';
import { isOutcomeNavigation } from '../outcome/is-outcome-navigation';
import { resolveOutcome } from '../outcome/resolve-outcome';
import { canMatchVersion } from '../version/can-match-version';
import { resolveCatalogueContext } from './resolve-catalogue-context';
import { resolveCatalogueEntry } from './resolve-catalogue-entry';

@Component({ template: '' })
class Probe {}

const LATEST = '16.19.1';
const OLDER = '15.14.1';
const META = catalogMetaOf({
  latest: LATEST,
  versions: [LATEST, '16.18.1', OLDER],
  languages: ['en_US', 'en_GB', 'fr_FR'],
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
  ],
});
const TRANSIENT = 'public, max-age=0, s-maxage=60';

const CATALOGUE: Routes = [
  {
    path: 'champions',
    resolve: { context: resolveCatalogueContext('champions') },
    component: Probe,
  },
  {
    path: 'champions/:id',
    resolve: { entry: resolveCatalogueEntry('champions') },
    component: Probe,
  },
  { path: 'items', resolve: { context: resolveCatalogueContext('items') }, component: Probe },
  { path: 'items/:id', resolve: { entry: resolveCatalogueEntry('items') }, component: Probe },
];

// The shape of app.routes.ts, reduced to the catalogue.
const ROUTES: Routes = [
  {
    path: ':locale',
    canMatch: [localeGuard],
    children: [
      {
        path: '**',
        canMatch: [isOutcomeNavigation],
        resolve: { outcome: resolveOutcome },
        component: Probe,
      },
      ...CATALOGUE,
      { path: ':version', canMatch: [canMatchVersion], children: CATALOGUE },
      { path: '**', resolve: { outcome: resolveOutcome }, component: Probe },
    ],
  },
];

interface Answer {
  readonly status: number;
  readonly body: unknown;
  readonly headers?: Record<string, string>;
}

const UNKNOWN_ENTITY: Answer = {
  status: HttpStatusCode.NotFound,
  body: { status: 404, code: 'unknown-entity' },
};

function details(canonicalPath: string, state: EntityState | null = null): Answer {
  return { status: HttpStatusCode.Ok, body: { canonicalPath, version: LATEST, state } };
}

// The state of an entity opened at OLDER: open, or closed at the version before the latest.
function stateOf(isOpen: boolean): EntityState {
  const opener = { version: OLDER, path: 'items/1036-long-sword' };
  return {
    openerVersion: OLDER,
    lastVersion: isOpen ? null : '16.18.1',
    isOpen,
    isOpener: true,
    kind: 'changed',
    changedFields: [],
    openerLink: opener,
    canonical: isOpen ? { version: null, path: opener.path } : opener,
  };
}

// The simulated API: known URLs answer from the table, any other detail is unknown.
function simulatedApi(answers: Record<string, Answer>, requested: string[]): HttpInterceptorFn {
  return (request) => {
    const url = request.urlWithParams;
    requested.push(url);
    const { status, body, headers } = answers[url] ?? UNKNOWN_ENTITY;
    const init = { status, url, headers: new HttpHeaders(headers ?? {}) };
    return status < HttpStatusCode.BadRequest
      ? of(new HttpResponse({ ...init, body }))
      : throwError(() => new HttpErrorResponse({ ...init, error: body }));
  };
}

interface Visit {
  readonly router: Router;
  readonly init: ResponseInit;
  readonly requested: string[];
  readonly leaf: ActivatedRouteSnapshot;
}

async function visit(
  url: string,
  answers: Record<string, Answer> = {},
  platform: 'server' | 'browser' = 'server',
): Promise<Visit> {
  const init: ResponseInit = { status: HttpStatusCode.Ok, headers: new Headers() };
  const requested: string[] = [];
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        ROUTES,
        withRouterConfig({
          paramsInheritanceStrategy: 'always',
          resolveNavigationPromiseOnError: true,
        }),
        withNavigationErrorHandler(handleNavigationError),
      ),
      provideLocationMocks(),
      provideHttpClient(
        withInterceptors([
          simulatedApi({ '/api/meta': { status: 200, body: META }, ...answers }, requested),
        ]),
      ),
      { provide: API_BASE_URL, useValue: '' },
      { provide: PLATFORM_ID, useValue: platform },
      { provide: RESPONSE_INIT, useValue: platform === 'server' ? init : null },
    ],
  });
  const router = TestBed.inject(Router);
  await router.navigateByUrl(url);
  let leaf = router.routerState.snapshot.root;
  while (leaf.firstChild) {
    leaf = leaf.firstChild;
  }
  return { router, init, requested, leaf };
}

function headerOf(init: ResponseInit, name: string): string | null {
  return new Headers(init.headers).get(name);
}

describe('catalogue lists (resolveCatalogueContext)', () => {
  it('renders the latest version at its short URL, cached thirty minutes', async () => {
    const { leaf, init, requested } = await visit('/en/champions');

    expect(leaf.data['context']).toEqual({
      locale: 'en',
      version: LATEST,
      pinned: false,
      language: 'en_US',
    });
    expect(init.status).toBe(HttpStatusCode.Ok);
    expect(headerOf(init, 'Cache-Control')).toBe(
      'public, max-age=0, s-maxage=1800, stale-while-revalidate=86400',
    );
    expect(requested).toEqual(['/api/meta']);
  });

  it('renders an older version pinned in the path, cached a year', async () => {
    const { leaf, init } = await visit(`/fr/${OLDER}/items?page=2`);

    expect(leaf.data['context']).toEqual({
      locale: 'fr',
      version: OLDER,
      pinned: true,
      language: 'fr_FR',
    });
    expect(headerOf(init, 'Cache-Control')).toBe('public, max-age=3600, s-maxage=31536000');
  });

  it('moves the latest version pinned in the path to the short URL, with a 301', async () => {
    const { router, init, requested } = await visit(`/en/${LATEST}/champions?lang=en_GB`);

    expect(router.url).toBe(`/en/${LATEST}/champions?lang=en_GB`);
    expect(init.status).toBe(HttpStatusCode.MovedPermanently);
    expect(headerOf(init, 'Location')).toBe('/en/champions?lang=en_GB');
    expect(headerOf(init, 'Cache-Control')).toBe(TRANSIENT);
    expect(requested).toEqual(['/api/meta']);
  });

  it('moves a known ?version= into the path', async () => {
    const { init } = await visit(`/en/items?version=${OLDER}&page=2`);

    expect(init.status).toBe(HttpStatusCode.MovedPermanently);
    expect(headerOf(init, 'Location')).toBe(`/en/${OLDER}/items?page=2`);
  });

  it('answers a real 404 for a version Data Dragon does not list', async () => {
    const { leaf, init } = await visit('/en/99.99.1/champions');

    expect(leaf.data['outcome']).toEqual({ kind: 'not-found' });
    expect(init.status).toBe(HttpStatusCode.NotFound);
    expect(headerOf(init, 'Cache-Control')).toBe(TRANSIENT);
  });

  it('answers 503 before the first ingestion, unless the URL names a version', async () => {
    const empty = { '/api/meta': { status: 200, body: { ...META, latest: null } } };

    expect((await visit('/en/champions', empty)).init.status).toBe(
      HttpStatusCode.ServiceUnavailable,
    );
    TestBed.resetTestingModule();
    expect((await visit(`/en/${OLDER}/champions`, empty)).init.status).toBe(HttpStatusCode.Ok);
  });

  it('answers 503 when /api/meta cannot answer', async () => {
    const down = { '/api/meta': { status: 503, body: null, headers: { 'Retry-After': '5' } } };

    const { init } = await visit('/en/champions', down);

    expect(init.status).toBe(HttpStatusCode.ServiceUnavailable);
    expect(headerOf(init, 'Retry-After')).toBe('5');
  });
});

describe('catalogue details (resolveCatalogueEntry)', () => {
  const LONG_SWORD = `/api/catalog/${LATEST}/en_US/items/1036-long-sword`;

  it('hands the page the entity it fetched, in the variant of the query', async () => {
    const url = `/api/catalog/${LATEST}/en_GB/items/1036-long-sword`;
    const { leaf, init, requested } = await visit('/en/items/1036-long-sword?lang=en_GB', {
      [url]: details('items/1036-long-sword'),
    });

    expect(leaf.data['entry']).toEqual({
      context: { locale: 'en', version: LATEST, pinned: false, language: 'en_GB' },
      details: { canonicalPath: 'items/1036-long-sword', version: LATEST, state: null },
    });
    expect(init.status).toBe(HttpStatusCode.Ok);
    expect(requested).toEqual(['/api/meta', url]);
  });

  it('caches a page of the latest version thirty minutes, whatever its state', async () => {
    const { init } = await visit('/en/items/1036-long-sword', {
      [LONG_SWORD]: details('items/1036-long-sword', stateOf(false)),
    });

    expect(headerOf(init, 'Cache-Control')).toBe(
      'public, max-age=0, s-maxage=1800, stale-while-revalidate=86400',
    );
  });

  // The class by state (docs/produit/seo-une-page-par-etat.md, § 3.7): a closed state's page
  // never changes again; the open state's, or one whose state is unknown, changes once.
  it.each([
    ['closed', stateOf(false), 'public, max-age=3600, s-maxage=31536000'],
    ['open', stateOf(true), 'public, max-age=3600, s-maxage=3600, stale-while-revalidate=3600'],
    ['unknown', null, 'public, max-age=3600, s-maxage=3600, stale-while-revalidate=3600'],
  ])('caches a pinned page of a %s state by its class', async (_kind, state, cacheControl) => {
    const { init } = await visit(`/en/${OLDER}/items/1036-long-sword`, {
      [`/api/catalog/${OLDER}/en_US/items/1036-long-sword`]: details(
        'items/1036-long-sword',
        state,
      ),
    });

    expect(init.status).toBe(HttpStatusCode.Ok);
    expect(headerOf(init, 'Cache-Control')).toBe(cacheControl);
  });

  it('adds a missing slug with a 301, the id deciding', async () => {
    const { init } = await visit('/en/items/1036', {
      [`/api/catalog/${LATEST}/en_US/items/1036`]: details('items/1036-long-sword'),
    });

    expect(init.status).toBe(HttpStatusCode.MovedPermanently);
    expect(headerOf(init, 'Location')).toBe('/en/items/1036-long-sword');
  });

  it('never asks the API about the latest version pinned in the path', async () => {
    const { init, requested } = await visit(`/en/${LATEST}/items/1036-long-sword`, {
      [LONG_SWORD]: details('items/1036-long-sword'),
    });

    expect(headerOf(init, 'Location')).toBe('/en/items/1036-long-sword');
    expect(requested).toEqual(['/api/meta']);
  });

  it('sends an entity missing from a pinned version to that version list, with a 302', async () => {
    const { init } = await visit(`/en/${OLDER}/champions/Nope?lang=en_GB`);

    expect(init.status).toBe(HttpStatusCode.Found);
    expect(headerOf(init, 'Location')).toBe(`/en/${OLDER}/champions?lang=en_GB`);
  });

  it('answers a real 404 for an entity missing from the latest version', async () => {
    const { router, leaf, init } = await visit('/en/champions/Nope');

    expect(router.url).toBe('/en/champions/Nope');
    expect(leaf.data['outcome']).toEqual({ kind: 'not-found' });
    expect(init.status).toBe(HttpStatusCode.NotFound);
    expect(headerOf(init, 'X-Robots-Tag')).toBe('noindex');
  });

  it('answers 503 with the Retry-After of a version not ingested yet', async () => {
    const pending: Record<string, Answer> = {
      [`/api/catalog/${OLDER}/en_US/champions/Aatrox`]: {
        status: HttpStatusCode.ServiceUnavailable,
        body: { status: 503, code: 'catalog-pending' },
        headers: { 'Retry-After': '5' },
      },
    };

    const { init } = await visit(`/en/${OLDER}/champions/Aatrox`, pending);

    expect(init.status).toBe(HttpStatusCode.ServiceUnavailable);
    expect(headerOf(init, 'Retry-After')).toBe('5');
  });

  it('follows the redirect in the browser, then renders the canonical page', async () => {
    const answers = {
      [`/api/catalog/${LATEST}/en_US/items/1036`]: details('items/1036-long-sword'),
      [LONG_SWORD]: details('items/1036-long-sword'),
    };

    const { router, leaf } = await visit('/en/items/1036', answers, 'browser');

    expect(router.url).toBe('/en/items/1036-long-sword');
    expect(leaf.data['entry']).toMatchObject({
      details: { canonicalPath: 'items/1036-long-sword' },
    });
  });
});
