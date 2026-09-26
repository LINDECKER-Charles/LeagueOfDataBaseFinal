import { HttpStatusCode, provideHttpClient } from '@angular/common/http';
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
import type { DonationOptions } from '../../core/api/generated/models/donation-options';
import { activateLocale } from '../../core/i18n/activate-locale';
import { LOCALES } from '../../core/i18n/locales';
import { CANONICAL_ORIGIN } from '../../core/seo/canonical-origin';
import { Seo } from '../../core/seo/seo';
import { StripeRedirect } from './checkout/stripe-redirect';
import { DONATE_ROUTES } from './donate.routes';

const API = 'https://api.example.com';
const OPTIONS_URL = `${API}/api/donations/options`;
const CHECKOUT_URL = `${API}/api/donations/checkout`;
const STRIPE_PAGE = 'https://checkout.stripe.com/c/pay/cs_test_spec';
const OPEN: DonationOptions = {
  available: true,
  currency: 'eur',
  presets: [300, 500, 1_000, 2_500],
  minCents: 100,
  maxCents: 50_000,
};
const CATALOGUES: Record<string, Translation> = {
  'seo/fr': { donate: { title: 'Soutenir le projet', description: 'Un don garde le site.' } },
  'donate/fr': { title: 'Soutenir League Of Data Base' },
  fr: { header: { navigation: { home: 'Accueil' } } },
};

type Answer = (request: TestRequest) => void;

// The donation page below its locale, as app.routes.ts mounts it; its options answered by
// `answer`, Stripe's page never really left for.
async function visit(answer: Answer) {
  const go = vi.fn((url: string) => url.startsWith('https:'));
  TestBed.configureTestingModule({
    providers: [
      { provide: API_BASE_URL, useValue: API },
      { provide: CANONICAL_ORIGIN, useValue: 'https://league-of-data-base.com' },
      { provide: StripeRedirect, useValue: { go } },
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([
        {
          path: ':locale',
          resolve: { locale: activateLocale },
          children: [{ path: 'donate', children: DONATE_ROUTES }],
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
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  const apply = vi.spyOn(TestBed.inject(Seo), 'apply');
  const created = RouterTestingHarness.create('/fr/donate');
  answer(await vi.waitFor(() => http.expectOne(OPTIONS_URL)));
  const harness = await created;
  await harness.fixture.whenStable();
  return { page: harness.routeNativeElement as HTMLElement, harness, http, go, apply };
}

function form(page: HTMLElement): HTMLFormElement {
  return page.querySelector('form')!;
}

function typeAmount(page: HTMLElement, text: string): void {
  const input = page.querySelector<HTMLInputElement>('#donate-amount')!;
  input.value = text;
  input.dispatchEvent(new Event('input'));
}

function submit(page: HTMLElement): void {
  form(page).dispatchEvent(new Event('submit', { cancelable: true }));
}

function alertOf(page: HTMLElement): string | undefined {
  return page.querySelector('[role="alert"]')?.textContent?.trim();
}

describe('DonatePage', () => {
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

  it('offers the tiers of the API, the second one chosen', async () => {
    const { page } = await visit((request) => request.flush(OPEN));
    const tiers = [...page.querySelectorAll<HTMLInputElement>('input[name="preset"]')];

    expect(tiers.map((tier) => tier.value)).toEqual(['300', '500', '1000', '2500']);
    expect(tiers.map((tier) => tier.checked)).toEqual([false, true, false, false]);
    expect(page.querySelector('.donate-tier__amount')?.textContent?.trim()).toBe('3€');
    expect(page.querySelector('button[type="submit"]')?.hasAttribute('disabled')).toBe(false);
  });

  it('opens the checkout of the chosen tier, then follows Stripe', async () => {
    const { page, harness, http, go } = await visit((request) => request.flush(OPEN));
    page.querySelector<HTMLInputElement>('input[value="1000"]')!.click();

    submit(page);
    const request = http.expectOne(CHECKOUT_URL);
    request.flush({ url: STRIPE_PAGE });
    await harness.fixture.whenStable();

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ amountCents: 1_000, locale: 'fr' });
    expect(go).toHaveBeenCalledExactlyOnceWith(STRIPE_PAGE);
    expect(alertOf(page)).toBeUndefined();
  });

  it('gives the free amount over the tier, with either decimal separator', async () => {
    const { page, harness, http } = await visit((request) => request.flush(OPEN));
    typeAmount(page, '7,50');

    submit(page);
    const request = http.expectOne(CHECKOUT_URL);
    request.flush({ url: STRIPE_PAGE });
    await harness.fixture.whenStable();

    expect(request.request.body).toEqual({ amountCents: 750, locale: 'fr' });
  });

  it.each(['0,50', '500.01', 'dix euros'])('refuses %j without asking the API', async (typed) => {
    const { page, harness, go } = await visit((request) => request.flush(OPEN));
    typeAmount(page, typed);

    submit(page);
    await harness.fixture.whenStable();

    expect(alertOf(page)).toBe('donate.error.invalid_amount');
    expect(page.querySelector('#donate-amount')?.getAttribute('aria-invalid')).toBe('true');
    expect(go).not.toHaveBeenCalled();
  });

  it.each([
    [HttpStatusCode.BadRequest, 'donate.error.invalid_amount'],
    [HttpStatusCode.Forbidden, 'donate.error.csrf'],
    [HttpStatusCode.TooManyRequests, 'donate.error.throttled'],
    [HttpStatusCode.ServiceUnavailable, 'donate.error.unavailable'],
    [HttpStatusCode.BadGateway, 'donate.error.gateway'],
    [HttpStatusCode.InternalServerError, 'donate.error.gateway'],
  ])('tells a %i refusal as %s, the form kept', async (status, message) => {
    const { page, harness, http, go } = await visit((request) => request.flush(OPEN));

    submit(page);
    http.expectOne(CHECKOUT_URL).flush({ status }, { status, statusText: 'Refused' });
    await harness.fixture.whenStable();

    expect(alertOf(page)).toBe(message);
    expect(go).not.toHaveBeenCalled();
    expect(page.querySelector('button[type="submit"]')?.hasAttribute('disabled')).toBe(false);
  });

  it('stays on the form when the answer is no secure address', async () => {
    const { page, harness, http, go } = await visit((request) => request.flush(OPEN));

    submit(page);
    http.expectOne(CHECKOUT_URL).flush({ url: 'javascript:alert(1)' });
    await harness.fixture.whenStable();

    expect(go).toHaveBeenCalledExactlyOnceWith('javascript:alert(1)');
    expect(alertOf(page)).toBe('donate.error.gateway');
  });

  it.each<[string, Answer]>([
    ['Stripe is not set up', (request) => request.flush({ ...OPEN, available: false })],
    ['the API fails', (request) => request.flush(null, { status: 500, statusText: 'Down' })],
    ['the answer is no options', (request) => request.flush({ latest: '16.19.1' })],
  ])('closes the form when %s', async (_, answer) => {
    const { page } = await visit(answer);

    expect(page.querySelector('form')).toBeNull();
    expect(page.querySelector('[role="status"]')?.textContent).toContain(
      'donate.unavailable.title',
    );
    expect(page.querySelector('.donate-unavailable button')?.hasAttribute('disabled')).toBe(true);
  });

  it('writes an indexable head with its breadcrumb', async () => {
    const { page, apply } = await visit((request) => request.flush(OPEN));

    const head = apply.mock.lastCall![0];
    expect(head).toMatchObject({
      title: 'Soutenir le projet',
      description: 'Un don garde le site.',
      path: 'donate',
    });
    expect(head).not.toHaveProperty('kind');
    expect(page.querySelector('h1')?.textContent?.trim()).toBe('Soutenir League Of Data Base');
    const links = [...page.querySelectorAll('.donate-legal a')].map((a) => a.getAttribute('href'));
    expect(links).toEqual(['/fr/legal/privacy', '/fr/legal/terms']);
  });
});
