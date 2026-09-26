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
import { provideRouter, Router, type Routes } from '@angular/router';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { API_BASE_URL } from '../../../../core/api/api-base-url';
import type { SharedBuild } from '../../../../core/api/generated/models/shared-build';
import { isOutcomeNavigation } from '../../../../core/routing/outcome/is-outcome-navigation';
import { resolveOutcome } from '../../../../core/routing/outcome/resolve-outcome';
import { sharedBuildOf } from '../testing/shared-build-of';
import { resolveSharedBuild } from './resolve-shared-build';

@Component({ template: '' })
class Probe {}

const TOKEN = '0123456789abcdef01234567';
const TRANSIENT = 'public, max-age=0, s-maxage=60';
const SHARED_PATH = /^\/api\/share\/([^?]+)/;

// The shape of app.routes.ts, reduced to the shared builds: an outcome renders in place.
const ROUTES: Routes = [
  {
    path: 'b',
    children: [
      {
        path: '**',
        canMatch: [isOutcomeNavigation],
        resolve: { outcome: resolveOutcome },
        component: Probe,
      },
      { path: ':token', resolve: { shared: resolveSharedBuild }, component: Probe },
    ],
  },
];

// The simulated API: the build of TOKEN; any other token is the API's 404.
function simulatedApi(
  build: SharedBuild,
  failure: HttpStatusCode | null,
  requested: string[],
): HttpInterceptorFn {
  return (request) => {
    const url = request.urlWithParams;
    requested.push(url);
    const token = SHARED_PATH.exec(url)?.[1];
    const status = failure ?? (token === TOKEN ? null : HttpStatusCode.NotFound);
    if (status !== null) {
      return throwError(() => new HttpErrorResponse({ status, url }));
    }
    return of(new HttpResponse({ status: HttpStatusCode.Ok, url, body: build }));
  };
}

async function visit(url: string, build = sharedBuildOf(), failure: HttpStatusCode | null = null) {
  const init: ResponseInit = { status: HttpStatusCode.Ok, headers: new Headers() };
  const requested: string[] = [];
  TestBed.configureTestingModule({
    providers: [
      provideRouter(ROUTES),
      provideLocationMocks(),
      provideHttpClient(withInterceptors([simulatedApi(build, failure, requested)])),
      provideTransloco({
        config: { availableLangs: ['en', 'fr', 'zh-hant'], defaultLang: 'en', prodMode: true },
        loader: class {
          getTranslation = () => of({});
        },
      }),
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
  const { shared, outcome } = leaf.data;
  return { shared: shared as unknown, outcome: outcome as unknown, init, requested, navigated };
}

describe('resolveSharedBuild', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
  });

  it('reads the build on its own patch and speaks its language, cached a minute', async () => {
    const build = sharedBuildOf();

    const { shared, requested, init } = await visit(`/b/${TOKEN}`, build);

    expect(shared).toEqual({ build, locale: 'fr' });
    expect(requested).toEqual([`/api/share/${TOKEN}`]);
    expect(document.documentElement.lang).toBe('fr');
    expect(TestBed.inject(TranslocoService).getActiveLang()).toBe('fr');
    expect((init.headers as Headers).get('Cache-Control')).toBe(TRANSIENT);
  });

  it.each([
    ['zh_TW', 'zh-hant'],
    ['nl_NL', 'en'],
  ])('speaks %s builds in %s', async (language, locale) => {
    const { shared } = await visit(`/b/${TOKEN}`, sharedBuildOf({ language }));

    expect(shared).toEqual(expect.objectContaining({ locale }));
    expect(document.documentElement.lang).toBe(locale);
  });

  it('passes ?version= and ?lang= to the API as they are', async () => {
    const { requested } = await visit(`/b/${TOKEN}?version=16.19.1&lang=en_GB`);

    expect(requested).toEqual([`/api/share/${TOKEN}?version=16.19.1&lang=en_GB`]);
  });

  it.each([
    ['a malformed token', '/b/NOT-A-TOKEN', null],
    ['a token no build holds', '/b/ffffffffffffffffffffffff', null],
    ['a context the API refuses', `/b/${TOKEN}?version=0.0`, HttpStatusCode.BadRequest],
  ])('answers the 404 in place for %s', async (_case, url, failure) => {
    const { shared, outcome, init } = await visit(url, sharedBuildOf(), failure);

    expect(shared).toBeUndefined();
    expect(outcome).toEqual({ kind: 'not-found' });
    expect(init.status).toBe(HttpStatusCode.NotFound);
  });

  it('fails the navigation when the API is out of reach, for the error page to answer', async () => {
    const { navigated, shared } = await visit(
      `/b/${TOKEN}`,
      sharedBuildOf(),
      HttpStatusCode.ServiceUnavailable,
    );

    expect(navigated).toBeInstanceOf(HttpErrorResponse);
    expect(shared).toBeUndefined();
  });
});
