import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  type TestRequest,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, type Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-base-url';
import type { PublicApiReference } from '../../core/api/generated/models/public-api-reference';
import { activateLocale } from '../../core/i18n/activate-locale';
import { LOCALES } from '../../core/i18n/locales';
import { CANONICAL_ORIGIN } from '../../core/seo/canonical-origin';
import { Seo } from '../../core/seo/seo';
import { DEVELOPERS_ROUTES } from './developers.routes';

const API = 'https://api.example.com';
const REFERENCE_URL = `${API}/api/public-api/reference`;
const SITE = 'https://league-of-data-base.com';
const REFERENCE: PublicApiReference = {
  baseUrl: 'https://api.example.test',
  keyPrefix: 'lodb_',
  freePlan: { monthlyQuota: 500, ratePerMinute: 10 },
  creditsRatePerMinute: 60,
  packs: [{ code: 'small', priceCents: 500, requests: 5_000 }],
  plans: [
    {
      code: 'monthly',
      interval: 'month',
      monthlyQuota: 15_000,
      priceCents: 900,
      ratePerMinute: 60,
    },
  ],
};
const CATALOGUES: Record<string, Translation> = {
  'seo/fr': { developers: { title: 'API pour développeurs', description: 'Une API REST.' } },
  'api/fr': { nav: { developers: 'API' }, developers: { title: "L'API LeagueOfDataBase" } },
  'developers/fr': { base_url: 'URL de base : {{ url }}' },
  fr: { header: { navigation: { home: 'Accueil' } } },
};

type Answer = (request: TestRequest) => void;

// Everything the page needs but a real API.
function pageProviders() {
  return [
    { provide: API_BASE_URL, useValue: API },
    { provide: CANONICAL_ORIGIN, useValue: SITE },
    provideHttpClient(),
    provideHttpClientTesting(),
    provideRouter([
      {
        path: ':locale',
        resolve: { locale: activateLocale },
        children: [{ path: 'developers', children: DEVELOPERS_ROUTES }],
      },
    ]),
    provideTransloco({
      config: {
        availableLangs: [...LOCALES],
        defaultLang: 'en',
        missingHandler: { logMissingKey: false },
        prodMode: true,
      },
      loader: class {
        getTranslation = (path: string) => of(CATALOGUES[path] ?? {});
      },
    }),
  ];
}

// The documentation below its locale, as app.routes.ts mounts it, its reference answered
// by `answer`.
async function visit(answer: Answer) {
  TestBed.configureTestingModule({ providers: pageProviders() });
  const http = TestBed.inject(HttpTestingController);
  const apply = vi.spyOn(TestBed.inject(Seo), 'apply');
  const created = RouterTestingHarness.create('/fr/developers');
  answer(await vi.waitFor(() => http.expectOne(REFERENCE_URL)));
  const harness = await created;
  await harness.fixture.whenStable();
  return { page: harness.routeNativeElement as HTMLElement, apply };
}

function text(page: HTMLElement, selector: string): string | undefined {
  return page.querySelector(selector)?.textContent?.trim();
}

describe('DevelopersPage', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
    document.head.querySelectorAll('[data-lodb-seo]').forEach((element) => element.remove());
  });

  it('documents the base URL the API is configured with', async () => {
    const { page } = await visit((request) => request.flush(REFERENCE));

    expect(text(page, '[data-testid="developers-base-url"]')).toBe(
      'URL de base : https://api.example.test',
    );
    expect(text(page, '[data-testid="developers-curl"]')).toContain(
      '"https://api.example.test/v1/usage"',
    );
    expect(JSON.parse(text(page, '[data-testid="developers-usage"]')!)).toMatchObject({
      monthly_quota: 500,
      rate_limit_per_min: 10,
    });
  });

  it('lists the five routes of the API', async () => {
    const { page } = await visit((request) => request.flush(REFERENCE));

    const paths = [...page.querySelectorAll('.dev-endpoint code')].map((code) => code.textContent);
    expect(paths).toEqual([
      '/healthz',
      '/v1/profiles/{username}',
      '/v1/champions/{championId}/builds',
      '/v1/trends/{type}',
      '/v1/usage',
    ]);
  });

  it('prices the free plan, the packs and the plans', async () => {
    const { page } = await visit((request) => request.flush(REFERENCE));

    const offers = [...page.querySelectorAll('[data-testid="developers-pricing"] tbody tr')];
    expect(offers.map((row) => row.getAttribute('data-offer'))).toEqual([
      'free',
      'small',
      'monthly',
    ]);
    expect(offers[1]?.textContent).toContain('5 €');
    expect(offers[2]?.textContent).toContain('api.portal.billing.per_month');
  });

  it.each<[string, Answer]>([
    ['the API fails', (request) => request.flush(null, { status: 503, statusText: 'Down' })],
    ['the answer is no reference', (request) => request.flush({ baseUrl: 'javascript:1' })],
  ])('still documents the routes when %s, its prices withheld', async (_, answer) => {
    const { page } = await visit(answer);

    expect(text(page, '[data-testid="developers-base-url"]')).toBe(`URL de base : ${SITE}`);
    expect(page.querySelectorAll('.dev-endpoint')).toHaveLength(5);
    expect(page.querySelector('[data-testid="developers-pricing"]')).toBeNull();
    expect(page.querySelector('[data-testid="developers-usage"]')).toBeNull();
    expect(text(page, '[role="status"]')).toBe('developers.pricing_unavailable');
  });

  it('writes an indexable head with its breadcrumb, and leads to the portal', async () => {
    const { page, apply } = await visit((request) => request.flush(REFERENCE));

    const head = apply.mock.lastCall![0];
    expect(head).toMatchObject({
      title: 'API pour développeurs',
      description: 'Une API REST.',
      path: 'developers',
    });
    expect(head).not.toHaveProperty('kind');
    expect(text(page, 'h1')).toBe("L'API LeagueOfDataBase");
    const cta = page.querySelector('[data-testid="developers-cta"]');
    expect(cta?.getAttribute('href')).toBe('/fr/account/api');
  });
});
