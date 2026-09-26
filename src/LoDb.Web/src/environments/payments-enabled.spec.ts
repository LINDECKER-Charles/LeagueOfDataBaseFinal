import {
  type HttpInterceptorFn,
  HttpResponse,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { provideLocationMocks } from '@angular/common/testing';
import { PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  type ActivatedRouteSnapshot,
  provideRouter,
  Router,
  withRouterConfig,
} from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { routes } from '../app/app.routes';
import { API_BASE_URL } from '../app/core/api/api-base-url';
import type { CatalogMeta } from '../app/core/api/generated/models/catalog-meta';
import { DonatePage } from '../app/features/donate/donate-page';
import { ErrorPage } from '../app/features/errors/error-page';
import { environment } from './environment';
import { PAYMENTS_ENABLED } from './payments-enabled';

const META: CatalogMeta = {
  latest: '16.19.1',
  versions: ['16.19.1'],
  readyVersions: ['16.19.1'],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [{ locale: 'en', language: 'en_US' }],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};

// The API answers its meta, which the `:version` guard reads once `donate` falls through.
const simulatedApi: HttpInterceptorFn = (request) =>
  of(new HttpResponse({ status: HttpStatusCode.Ok, url: request.urlWithParams, body: META }));

interface Visit {
  readonly leaf: ActivatedRouteSnapshot;
  readonly init: ResponseInit;
}

async function visitDonate(payments: boolean): Promise<Visit> {
  const init: ResponseInit = { status: HttpStatusCode.Ok, headers: new Headers() };
  TestBed.configureTestingModule({
    providers: [
      provideRouter(routes, withRouterConfig({ paramsInheritanceStrategy: 'always' })),
      provideLocationMocks(),
      provideHttpClient(withInterceptors([simulatedApi])),
      provideTransloco({
        config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
        loader: class {
          getTranslation = () => of({});
        },
      }),
      { provide: API_BASE_URL, useValue: '' },
      { provide: PLATFORM_ID, useValue: 'server' },
      { provide: RESPONSE_INIT, useValue: init },
      { provide: PAYMENTS_ENABLED, useValue: payments },
    ],
  });
  const router = TestBed.inject(Router);
  await router.navigateByUrl('/en/donate');
  let leaf = router.routerState.snapshot.root;
  while (leaf.firstChild) {
    leaf = leaf.firstChild;
  }
  return { leaf, init };
}

describe('PAYMENTS_ENABLED', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
  });

  it('follows the build environment', () => {
    expect(TestBed.inject(PAYMENTS_ENABLED)).toBe(environment.payments);
  });

  it('routes the donation page in a build with payments', async () => {
    const { leaf, init } = await visitDonate(true);

    expect(leaf.component).toBe(DonatePage);
    expect(init.status).toBe(HttpStatusCode.Ok);
  });

  it('answers a 404 for the donation page in the store build (ADR 0007)', async () => {
    const { leaf, init } = await visitDonate(false);

    expect(leaf.component).toBe(ErrorPage);
    expect(init.status).toBe(HttpStatusCode.NotFound);
  });
});
