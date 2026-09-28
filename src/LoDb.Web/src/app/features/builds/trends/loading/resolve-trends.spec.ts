import {
  HttpErrorResponse,
  type HttpInterceptorFn,
  HttpResponse,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { provideLocationMocks } from '@angular/common/testing';
import { Component, PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, type Routes, withRouterConfig } from '@angular/router';
import { of, throwError } from 'rxjs';
import { API_BASE_URL } from '../../../../core/api/api-base-url';
import type { CatalogMeta } from '../../../../core/api/generated/models/catalog-meta';
import { isOutcomeNavigation } from '../../../../core/routing/outcome/is-outcome-navigation';
import { resolveOutcome } from '../../../../core/routing/outcome/resolve-outcome';
import { trendsPageOf } from '../testing/trends-page-of';
import { resolveTrends } from './resolve-trends';

@Component({ template: '' })
class Probe {}

const LATEST = '16.19.1';
const OLDER = '15.14.1';
const META: CatalogMeta = {
  latest: LATEST,
  versions: [LATEST, OLDER],
  readyVersions: [LATEST],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US', 'en_GB', 'fr_FR'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
  ],
  fallbackLocale: 'en',
  gameModes: [
    { mode: 'sr', map: 11 },
    { mode: 'aram', map: 12 },
  ],
  defaultGameMode: 'sr',
};
const TRANSIENT = 'public, max-age=0, s-maxage=60';
const PAGE = trendsPageOf();

// The shape of app.routes.ts, reduced to the trends: an outcome renders in place.
const ROUTES: Routes = [
  {
    path: ':locale',
    children: [
      {
        path: '**',
        canMatch: [isOutcomeNavigation],
        resolve: { outcome: resolveOutcome },
        component: Probe,
      },
      {
        path: 'trends',
        runGuardsAndResolvers: 'paramsOrQueryParamsChange',
        resolve: { trends: resolveTrends },
        component: Probe,
      },
    ],
  },
];

// The simulated API: its meta, and a page of trends, or the failure the case asks for.
function simulatedApi(failure: HttpStatusCode | null, requested: string[]): HttpInterceptorFn {
  return (request) => {
    const url = request.urlWithParams;
    requested.push(url);
    const trends = url.startsWith('/api/trends');
    if (trends && failure !== null) {
      return throwError(() => new HttpErrorResponse({ status: failure, url }));
    }
    const body = trends ? PAGE : META;
    return of(new HttpResponse({ status: HttpStatusCode.Ok, url, body }));
  };
}

async function visit(url: string, failure: HttpStatusCode | null = null) {
  const init: ResponseInit = { status: HttpStatusCode.Ok, headers: new Headers() };
  const requested: string[] = [];
  TestBed.configureTestingModule({
    providers: [
      provideRouter(ROUTES, withRouterConfig({ paramsInheritanceStrategy: 'always' })),
      provideLocationMocks(),
      provideHttpClient(withInterceptors([simulatedApi(failure, requested)])),
      { provide: API_BASE_URL, useValue: '' },
      { provide: PLATFORM_ID, useValue: 'server' },
      { provide: RESPONSE_INIT, useValue: init },
    ],
  });
  const router = TestBed.inject(Router);
  const navigated = await router.navigateByUrl(url).catch((error: unknown) => error);
  let leaf = router.routerState.snapshot.root;
  while (leaf.firstChild) {
    leaf = leaf.firstChild;
  }
  const { trends, outcome } = leaf.data;
  // The reads of the trends so far, the meta left out.
  const reads = () => requested.filter((entry) => entry.startsWith('/api/trends'));
  return { trends: trends as unknown, outcome: outcome as unknown, init, reads, navigated, router };
}

describe('resolveTrends', () => {
  it('reads the first page in the latest version and the locale language, cached a minute', async () => {
    const { trends, reads, init } = await visit('/fr/trends');

    expect(trends).toEqual({
      locale: 'fr',
      page: PAGE,
      modes: ['sr', 'aram'],
      request: { version: LATEST, lang: 'fr_FR' },
    });
    expect(reads()).toEqual([`/api/trends?version=${LATEST}&lang=fr_FR`]);
    expect((init.headers as Headers).get('Cache-Control')).toBe(TRANSIENT);
  });

  it('passes the filters and the page of the URL to the API', async () => {
    const { reads } = await visit('/en/trends?champion=MonkeyKing&mode=aram&language=fr_FR&page=2');

    expect(reads()).toEqual([
      `/api/trends?champion=MonkeyKing&mode=aram&language=fr_FR&page=2&version=${LATEST}&lang=en_US`,
    ]);
  });

  it('leaves out blank filters and a page that is no page', async () => {
    const { reads } = await visit('/en/trends?champion=&mode=%20&page=zero');

    expect(reads()).toEqual([`/api/trends?version=${LATEST}&lang=en_US`]);
  });

  it('follows ?version= and ?lang= for the names of the builds', async () => {
    const { reads } = await visit(`/en/trends?version=${OLDER}&lang=en_GB`);

    expect(reads()).toEqual([`/api/trends?version=${OLDER}&lang=en_GB`]);
  });

  it('reads the trends again when only the query changes', async () => {
    const { reads, router } = await visit('/en/trends');

    await router.navigateByUrl('/en/trends?mode=aram');

    expect(reads()).toEqual([
      `/api/trends?version=${LATEST}&lang=en_US`,
      `/api/trends?mode=aram&version=${LATEST}&lang=en_US`,
    ]);
  });

  it('answers the 404 in place for a context the API refuses', async () => {
    const { trends, outcome, init } = await visit('/en/trends', HttpStatusCode.BadRequest);

    expect(trends).toBeUndefined();
    expect(outcome).toEqual({ kind: 'not-found' });
    expect(init.status).toBe(HttpStatusCode.NotFound);
  });

  it('fails the navigation when the API is out of reach, for the error page to answer', async () => {
    const { navigated, trends } = await visit('/en/trends', HttpStatusCode.ServiceUnavailable);

    expect(navigated).toBeInstanceOf(HttpErrorResponse);
    expect(trends).toBeUndefined();
  });
});
