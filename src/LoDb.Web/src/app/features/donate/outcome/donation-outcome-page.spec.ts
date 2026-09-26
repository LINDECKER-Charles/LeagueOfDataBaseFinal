import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, type Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import { activateLocale } from '../../../core/i18n/activate-locale';
import { LOCALES } from '../../../core/i18n/locales';
import { CANONICAL_ORIGIN } from '../../../core/seo/canonical-origin';
import { Seo } from '../../../core/seo/seo';
import { DONATE_ROUTES } from '../donate.routes';

const CATALOGUES: Record<string, Translation> = {
  'donate/de': { success: { title: 'Danke, Beschwörer' }, cancel: { title: 'Spende abgebrochen' } },
};

// The return pages below their locale, as app.routes.ts mounts them. They ask the API nothing.
async function visit(url: string) {
  TestBed.configureTestingModule({
    providers: [
      { provide: API_BASE_URL, useValue: 'https://api.example.com' },
      { provide: CANONICAL_ORIGIN, useValue: 'https://league-of-data-base.com' },
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
  const apply = vi.spyOn(TestBed.inject(Seo), 'apply');
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
  return { page: harness.routeNativeElement as HTMLElement, apply };
}

function hrefs(page: HTMLElement): (string | null)[] {
  return [...page.querySelectorAll('a')].map((link) => link.getAttribute('href'));
}

describe('DonationOutcomePage', () => {
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

  it('thanks the donor under a lit seal, back to the encyclopedia', async () => {
    const { page } = await visit('/de/donate/success?session_id=cs_test_1');

    expect(page.querySelector('h1')?.textContent?.trim()).toBe('Danke, Beschwörer');
    expect(page.querySelector('.donate-seal')?.classList).toContain('donate-seal--lit');
    expect(hrefs(page)).toEqual(['/de']);
  });

  it('offers to try again after a cancel, under a dormant seal', async () => {
    const { page } = await visit('/de/donate/cancel');

    expect(page.querySelector('h1')?.textContent?.trim()).toBe('Spende abgebrochen');
    expect(page.querySelector('.donate-seal')?.classList).not.toContain('donate-seal--lit');
    expect(hrefs(page)).toEqual(['/de/donate', '/de']);
  });

  it.each([
    ['success', 'Danke, Beschwörer'],
    ['cancel', 'Spende abgebrochen'],
  ])('keeps donate/%s out of the index, its links followed', async (outcome, title) => {
    const { apply } = await visit(`/de/donate/${outcome}`);

    expect(apply).toHaveBeenLastCalledWith({
      title,
      titleFormat: 'account',
      kind: 'donation-return',
      path: 'donate',
    });
    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(
      'noindex, follow',
    );
  });
});
