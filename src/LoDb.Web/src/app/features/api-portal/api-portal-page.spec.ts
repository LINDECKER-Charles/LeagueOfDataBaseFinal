import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-base-url';
import type { AccountUser } from '../../core/api/generated/models/account-user';
import type { ApiKeyOverview } from '../../core/api/generated/models/api-key-overview';
import type { BillingOffers } from '../../core/api/generated/models/billing-offers';
import { AUTH_STRATEGY } from '../../core/auth/strategy/auth-strategy-token';
import { accountUser } from '../../core/auth/testing/account-user';
import { FakeAuthStrategy } from '../../core/auth/testing/fake-auth-strategy';
import { activateLocale } from '../../core/i18n/activate-locale';
import { LOCALES } from '../../core/i18n/locales';
import { CANONICAL_ORIGIN } from '../../core/seo/canonical-origin';
import { Seo } from '../../core/seo/seo';
import { API_PORTAL_ROUTES } from './api-portal.routes';
import { PORTAL_PAYMENTS } from './shared/portal-payments';
import { PortalStripeRedirect } from './state/stripe-redirect';

const API = 'https://api.example.com';
const KEY_URL = `${API}/api/account/api-key`;
const REGENERATE_URL = `${KEY_URL}/regenerate`;
const OFFERS_URL = `${API}/api/billing/offers`;
const PACK_URL = `${API}/api/billing/checkout/pack`;
const STRIPE_PAGE = 'https://checkout.stripe.com/c/pay/cs_test_portal';
const SECRET = 'lodb_0123456789abcdef0123456789abcdef01234567';
const KEY: ApiKeyOverview = {
  prefix: 'lodb_0123456',
  name: 'default',
  createdAt: '2026-09-01T10:00:00+00:00',
  plan: 'free',
  monthlyQuota: 500,
  usedThisMonth: 137,
  remainingThisMonth: 363,
  creditsBalance: 0,
  rateLimitPerMin: 10,
  subscribed: false,
  usage: [{ day: '2026-09-26', requests: 37 }],
};
const OFFERS: BillingOffers = {
  available: true,
  currency: 'eur',
  packs: [{ code: 'small', priceCents: 500, requests: 10_000 }],
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

interface Visit {
  readonly key?: ApiKeyOverview | null;
  readonly user?: AccountUser;
  readonly payments?: boolean;
  readonly url?: string;
  /** The first read of the key fails. */
  readonly failed?: boolean;
}

// Everything the portal needs but a real API, a real session and a real Stripe; without
// catalogues, every text renders as its key.
function portalProviders(user: AccountUser, payments: boolean, go: (target: string) => boolean) {
  const strategy = new FakeAuthStrategy();
  strategy.sessionAnswer = of({ user });
  return [
    { provide: PLATFORM_ID, useValue: 'browser' },
    { provide: AUTH_STRATEGY, useValue: strategy },
    { provide: API_BASE_URL, useValue: API },
    { provide: CANONICAL_ORIGIN, useValue: 'https://league-of-data-base.com' },
    { provide: PORTAL_PAYMENTS, useValue: payments },
    { provide: PortalStripeRedirect, useValue: { go } },
    provideHttpClient(),
    provideHttpClientTesting(),
    provideRouter([
      {
        path: ':locale',
        resolve: { locale: activateLocale },
        children: [{ path: 'account/api', children: API_PORTAL_ROUTES }],
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
        getTranslation = () => of({});
      },
    }),
  ];
}

// The portal below its locale, as app.routes.ts mounts it, for a signed-in account. Stripe's
// page is never really left for.
async function visit(options: Visit = {}) {
  const { key = KEY, user = accountUser(), payments = true, url, failed } = options;
  const go = vi.fn((target: string) => target.startsWith('https:'));
  TestBed.configureTestingModule({ providers: portalProviders(user, payments, go) });
  const http = TestBed.inject(HttpTestingController);
  const apply = vi.spyOn(TestBed.inject(Seo), 'apply');
  const created = RouterTestingHarness.create(url ?? '/fr/account/api');
  const read = await vi.waitFor(() => http.expectOne(KEY_URL));
  if (failed) {
    read.flush(null, { status: 500, statusText: 'Down' });
  } else {
    read.flush({ key });
  }
  if (payments) {
    http.expectOne(OFFERS_URL).flush(OFFERS);
  }
  const harness = await created;
  await harness.fixture.whenStable();
  const settle = () => harness.fixture.whenStable();
  return { page: harness.routeNativeElement as HTMLElement, settle, http, go, apply };
}

function text(page: HTMLElement, selector: string): string | undefined {
  return page.querySelector(selector)?.textContent?.trim();
}

function buttonOf(page: HTMLElement, label: string): HTMLButtonElement {
  const buttons = [...page.querySelectorAll<HTMLButtonElement>('button')];
  return buttons.find((button) => button.textContent?.trim() === label)!;
}

function submitName(page: HTMLElement, name: string): void {
  const input = page.querySelector<HTMLInputElement>('input[name="name"]')!;
  input.value = name;
  input.dispatchEvent(new Event('input'));
  page.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
}

describe('ApiPortalPage', () => {
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

  it('shows the key by its prefix, never a secret, with its usage and offers', async () => {
    const { page } = await visit();

    expect(text(page, '[data-testid="api-key-prefix"]')).toBe('lodb_0123456…');
    expect(text(page, '[data-testid="api-key-plan"]')).toBe('api.plan.free');
    expect(page.querySelector('[data-testid="api-key-secret"]')).toBeNull();
    expect(page.querySelectorAll('[data-testid="api-usage"] tbody tr')).toHaveLength(1);
    expect(page.querySelector('[data-offer="small"]')).not.toBeNull();
    expect(page.querySelector('[data-offer="monthly"]')).not.toBeNull();
  });

  it('issues a key under the name typed, then shows its secret once', async () => {
    const { page, settle, http } = await visit({ key: null });

    submitName(page, '  bot  ');
    const request = http.expectOne(KEY_URL);
    request.flush({ secret: SECRET, key: KEY });
    await settle();

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'bot' });
    const field = page.querySelector<HTMLInputElement>('[data-testid="api-key-secret"]');
    expect(field?.value).toBe(SECRET);
    expect(text(page, '[data-testid="api-portal-notice"]')).toBe('api.portal.flash.created');

    buttonOf(page, 'apiPortal.raw.done').click();
    await settle();
    expect(page.querySelector('[data-testid="api-key-secret"]')).toBeNull();
    expect(text(page, '[data-testid="api-key-prefix"]')).toBe('lodb_0123456…');
  });

  it('sends an account whose e-mail is not verified to verify it first', async () => {
    const { page } = await visit({ key: null, user: accountUser({ emailVerified: false }) });

    expect(page.querySelector('form')).toBeNull();
    expect(page.textContent).toContain('auth.verify.gate_api');
    const verify = [...page.querySelectorAll('a')].find(
      (link) => link.textContent?.trim() === 'apiPortal.create.verify',
    );
    expect(verify?.getAttribute('href')).toBe('/fr/account/verify-email');
  });

  it('regenerates the secret, the new prefix shown', async () => {
    const { page, settle, http } = await visit();

    buttonOf(page, 'api.portal.actions.regenerate').click();
    const request = http.expectOne(REGENERATE_URL);
    request.flush({ secret: SECRET, key: { ...KEY, prefix: 'lodb_fedcba9' } });
    await settle();

    expect(request.request.method).toBe('POST');
    expect(text(page, '[data-testid="api-key-prefix"]')).toBe('lodb_fedcba9…');
    expect(page.querySelector<HTMLInputElement>('[data-testid="api-key-secret"]')?.value).toBe(
      SECRET,
    );
    expect(text(page, '[data-testid="api-portal-notice"]')).toBe('api.portal.flash.regenerated');
  });

  it('revokes the key, the form offered again', async () => {
    const { page, settle, http } = await visit();

    buttonOf(page, 'api.portal.actions.revoke').click();
    const request = http.expectOne(KEY_URL);
    request.flush(null, { status: 204, statusText: 'No Content' });
    await settle();

    expect(request.request.method).toBe('DELETE');
    expect(page.querySelector('[data-testid="api-key-prefix"]')).toBeNull();
    expect(page.querySelector('form')).not.toBeNull();
    expect(text(page, '[data-testid="api-portal-notice"]')).toBe('api.portal.flash.revoked');
  });

  it('tells a refusal by its code, the key kept', async () => {
    const { page, settle, http } = await visit({ key: null });

    submitName(page, '');
    const problem = { status: 409, code: 'api-key-exists' };
    http.expectOne(KEY_URL).flush(problem, { status: 409, statusText: 'Conflict' });
    await settle();

    expect(text(page, '[role="alert"]')).toBe('api.portal.flash.key_exists');
    expect(page.querySelector('[data-testid="api-key-secret"]')).toBeNull();
  });

  it('buys a pack in the page locale, then follows Stripe', async () => {
    const { page, settle, http, go } = await visit();

    page.querySelector<HTMLButtonElement>('[data-offer="small"] button')!.click();
    const request = http.expectOne(PACK_URL);
    request.flush({ url: STRIPE_PAGE });
    await settle();

    expect(request.request.body).toEqual({ pack: 'small', locale: 'fr' });
    expect(go).toHaveBeenCalledExactlyOnceWith(STRIPE_PAGE);
  });

  it('announces a return from Stripe, and ignores an unknown status', async () => {
    const back = await visit({ url: '/fr/account/api?status=pack_success' });
    expect(text(back.page, '[data-testid="api-portal-notice"]')).toBe(
      'api.portal.status.pack_success',
    );

    TestBed.inject(HttpTestingController).verify();
    TestBed.resetTestingModule();
    const forged = await visit({ url: '/fr/account/api?status=api.portal.title' });
    expect(forged.page.querySelector('[data-testid="api-portal-notice"]')).toBeNull();
  });

  it('sells nothing where the build does not, the key still managed', async () => {
    const { page } = await visit({ payments: false });

    expect(page.querySelector('lodb-offers-panel')).toBeNull();
    expect(text(page, '[data-testid="api-key-prefix"]')).toBe('lodb_0123456…');
  });

  it('offers to retry a read that failed', async () => {
    const { page, settle, http } = await visit({ payments: false, failed: true });
    expect(text(page, '[role="alert"] p')).toBe('apiPortal.error.load');

    buttonOf(page, 'apiPortal.retry').click();
    http.expectOne(KEY_URL).flush({ key: KEY });
    await settle();

    expect(page.querySelector('[role="alert"]')).toBeNull();
    expect(text(page, '[data-testid="api-key-prefix"]')).toBe('lodb_0123456…');
  });

  it('writes a private head', async () => {
    const { apply } = await visit();

    expect(apply.mock.lastCall![0]).toMatchObject({ kind: 'private', locale: 'fr' });
  });
});
