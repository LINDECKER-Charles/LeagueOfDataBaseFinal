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
import { API_BASE_URL } from '../../../core/api/api-base-url';
import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import type { PublicProfile } from '../../../core/api/generated/models/public-profile';
import { isOutcomeNavigation } from '../../../core/routing/outcome/is-outcome-navigation';
import { resolveOutcome } from '../../../core/routing/outcome/resolve-outcome';
import { resolvePublicProfile } from './resolve-public-profile';

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
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};
const TRANSIENT = 'public, max-age=0, s-maxage=60';
const CARD = { username: 'Faker', isPublic: true } as PublicProfile;
const PROFILE_PATH = /^\/api\/profiles\/([^?]+)/;

// The shape of app.routes.ts, reduced to the profile: an outcome renders in place.
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
      { path: 'u/:username', resolve: { profile: resolvePublicProfile }, component: Probe },
    ],
  },
];

// The simulated API: its meta, and the card of Faker; any other name is the API's 404.
function simulatedApi(failure: HttpStatusCode | null, requested: string[]): HttpInterceptorFn {
  return (request) => {
    const url = request.urlWithParams;
    requested.push(url);
    const username = PROFILE_PATH.exec(url)?.[1];
    const status =
      failure ?? (username === undefined || username === 'Faker' ? null : HttpStatusCode.NotFound);
    if (status !== null && username !== undefined) {
      return throwError(() => new HttpErrorResponse({ status, url }));
    }
    const body = username === undefined ? META : CARD;
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
  const { profile, outcome } = leaf.data;
  return { profile: profile as unknown, outcome: outcome as unknown, init, requested, navigated };
}

describe('resolvePublicProfile', () => {
  it('reads the card in the latest version and the locale language, cached a minute', async () => {
    const { profile, requested, init } = await visit('/fr/u/Faker');

    expect(profile).toEqual(CARD);
    expect(requested).toContain(`/api/profiles/Faker?version=${LATEST}&lang=fr_FR`);
    expect(init.status).toBe(HttpStatusCode.Ok);
    expect((init.headers as Headers).get('Cache-Control')).toBe(TRANSIENT);
  });

  it('follows ?version= and ?lang=, which the card then names its favorites in', async () => {
    const { requested } = await visit(`/en/u/Faker?version=${OLDER}&lang=en_GB`);

    expect(requested).toContain(`/api/profiles/Faker?version=${OLDER}&lang=en_GB`);
  });

  it.each([
    ['a name no public card answers to', '/en/u/nobody', null],
    ['a context the API refuses', '/en/u/Faker', HttpStatusCode.BadRequest],
  ])('answers the 404 in place for %s', async (_case, url, failure) => {
    const { profile, outcome, init } = await visit(url, failure);

    expect(profile).toBeUndefined();
    expect(outcome).toEqual({ kind: 'not-found' });
    expect(init.status).toBe(HttpStatusCode.NotFound);
    expect((init.headers as Headers).get('X-Robots-Tag')).toContain('noindex');
  });

  it('fails the navigation when the API is out of reach, for the error page to answer', async () => {
    const { navigated, profile } = await visit('/en/u/Faker', HttpStatusCode.ServiceUnavailable);

    expect(navigated).toBeInstanceOf(HttpErrorResponse);
    expect(profile).toBeUndefined();
  });
});
