import { HttpStatusCode } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, type ResolveFn } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, type Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { PageOutcome } from '../../core/routing/outcome/page-outcome';
import { CANONICAL_ORIGIN } from '../../core/seo/canonical-origin';
import { Seo } from '../../core/seo/seo';
import { ErrorPage } from './error-page';

const SEO: Translation = {
  error: {
    '404': { eyebrow: 'Lost', title: 'Page not found', body: 'Gone', back_home: 'Back home' },
    generic: { title: 'Something went wrong', body: 'Try again' },
  },
};
const BROKEN: PageOutcome = {
  kind: 'failure',
  status: HttpStatusCode.InternalServerError,
  retryAfter: null,
};

// Outcomes by URL, answered by one route as app.routes.ts does: the page is then reused.
function outcomeOf(outcomes: Record<string, PageOutcome>): ResolveFn<PageOutcome> {
  return (_route, state) => outcomes[state.url] ?? { kind: 'not-found' };
}

async function visit(url: string, outcomes: Record<string, PageOutcome> = {}) {
  TestBed.configureTestingModule({
    providers: [
      { provide: CANONICAL_ORIGIN, useValue: 'https://league-of-data-base.com' },
      provideRouter([
        {
          path: '**',
          runGuardsAndResolvers: 'always',
          resolve: { outcome: outcomeOf(outcomes) },
          component: ErrorPage,
        },
      ]),
      provideTransloco({
        config: {
          availableLangs: ['en', 'fr'],
          defaultLang: 'en',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = (path: string) => of(path.startsWith('seo/') ? SEO : {});
        },
      }),
    ],
  });
  const apply = vi.spyOn(TestBed.inject(Seo), 'apply');
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
  return { harness, apply };
}

function headingOf(harness: RouterTestingHarness): string | undefined {
  return harness.routeNativeElement?.querySelector('h1')?.textContent?.trim();
}

function linkOf(harness: RouterTestingHarness): string | null | undefined {
  return harness.routeNativeElement?.querySelector('a')?.getAttribute('href');
}

function headElements(selector: string): Element[] {
  return [...document.head.querySelectorAll(`[data-lodb-seo]${selector}`)];
}

describe('ErrorPage', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
    headElements('').forEach((element) => element.remove());
  });

  it('says the page is missing and leads back to the home of its locale', async () => {
    document.documentElement.lang = 'fr';

    const { harness } = await visit('/fr/nowhere');

    expect(headingOf(harness)).toBe('Page not found');
    expect(linkOf(harness)).toBe('/fr');
  });

  it.each([HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable] as const)(
    'says a %s went wrong',
    async (status) => {
      const { harness } = await visit('/down', { '/down': { ...BROKEN, status } });

      expect(headingOf(harness)).toBe('Something went wrong');
      expect(linkOf(harness)).toBe('/en');
    },
  );

  it('links a redirect body to its target, for a client that does not follow it', async () => {
    const location = '/en/items/1036-long-sword';
    const moved: PageOutcome = { kind: 'redirect', status: 301, location };

    const { harness } = await visit('/en/items/1036', { '/en/items/1036': moved });

    expect(headingOf(harness)).toBeUndefined();
    expect(linkOf(harness)).toBe(location);
  });

  it('follows the outcome of its route when the router reuses it', async () => {
    const { harness } = await visit('/nowhere', { '/broken': BROKEN });
    const page = harness.routeDebugElement?.componentInstance;

    await harness.navigateByUrl('/broken');
    await harness.fixture.whenStable();

    expect(harness.routeDebugElement?.componentInstance).toBe(page);
    expect(headingOf(harness)).toBe('Something went wrong');
  });

  it('writes a noindex head, with neither canonical nor alternates', async () => {
    document.documentElement.lang = 'fr';

    const { apply } = await visit('/fr/nowhere');

    expect(apply).toHaveBeenLastCalledWith({
      kind: 'error',
      locale: 'fr',
      title: 'Page not found',
    });
    expect(document.title).toBe('Page not found — League Of Data Base');
    expect(headElements('meta[name="robots"]')[0]?.getAttribute('content')).toBe(
      'noindex, nofollow',
    );
    expect(headElements('link[rel="canonical"], link[rel="alternate"]')).toEqual([]);
  });

  it('titles its head after the outcome it answers', async () => {
    const { harness, apply } = await visit('/nowhere', { '/broken': BROKEN });

    await harness.navigateByUrl('/broken');
    await harness.fixture.whenStable();

    expect(apply).toHaveBeenLastCalledWith({
      kind: 'error',
      locale: 'en',
      title: 'Something went wrong',
    });
    expect(document.title).toBe('Something went wrong — League Of Data Base');
  });
});
