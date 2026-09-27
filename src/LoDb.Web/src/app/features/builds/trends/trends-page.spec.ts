import {
  type HttpInterceptorFn,
  HttpResponse,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, type Translation } from '@jsverse/transloco';
import { provideTranslocoMessageformat } from '@jsverse/transloco-messageformat';
import { of } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import type { TrendsPage as TrendsPageModel } from '../../../core/api/generated/models/trends-page';
import { AuthSession } from '../../../core/auth/session/auth-session';
import type { SessionStatus } from '../../../core/auth/session/session-status';
import { CANONICAL_ORIGIN } from '../../../core/seo/canonical-origin';
import { Seo } from '../../../core/seo/seo';
import type { TrendsView } from './loading/trends-view';
import { trendsPageOf } from './testing/trends-page-of';
import { TrendsPage } from './trends-page';

const ORIGIN = 'https://league-of-data-base.com';
const CATALOGUES: Record<string, Translation> = {
  fr: {
    build: { mode: { sr: 'Faille', aram: 'ARAM' }, show: { by: 'Par {{ name }}' } },
    community: {
      trends: {
        title: 'Builds tendance',
        empty: 'Aucun build public.',
        empty_cta: 'Forger mon build',
        filter: { all_champions: 'Tous les champions', apply: 'Filtrer' },
      },
    },
    filter: {
      results: '{count, plural, one {# résultat} other {# résultats}}',
      page: 'page {{ page }} / {{ count }}',
      prev: 'Précédent',
      next: 'Suivant',
    },
    nav: { register: 'Créer un compte' },
  },
};

// The API of the browser's second read: the same page, with the reader's own votes.
function simulatedApi(fresh: TrendsPageModel, requested: string[]): HttpInterceptorFn {
  return (request) => {
    requested.push(request.urlWithParams);
    return of(new HttpResponse({ status: HttpStatusCode.Ok, url: request.url, body: fresh }));
  };
}

async function visit(page: TrendsPageModel, status: SessionStatus = 'unknown', url = '/fr/trends') {
  const requested: string[] = [];
  const fresh = {
    ...page,
    rows: page.rows.map((row) => ({ ...row, score: row.score + 1, myVote: 1 })),
  };
  const view: TrendsView = {
    locale: 'fr',
    page,
    modes: ['sr', 'aram'],
    request: { lang: 'fr_FR' },
  };
  document.documentElement.lang = 'fr';
  TestBed.configureTestingModule({
    providers: [
      { provide: CANONICAL_ORIGIN, useValue: ORIGIN },
      { provide: API_BASE_URL, useValue: '' },
      { provide: AuthSession, useValue: { status: signal(status) } },
      provideHttpClient(withInterceptors([simulatedApi(fresh, requested)])),
      provideRouter([
        {
          path: ':locale/trends',
          runGuardsAndResolvers: 'paramsOrQueryParamsChange',
          resolve: { trends: () => view },
          component: TrendsPage,
        },
      ]),
      provideTransloco({
        config: {
          availableLangs: ['en', 'fr'],
          defaultLang: 'fr',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = (path: string) => of(CATALOGUES[path] ?? {});
        },
      }),
      provideTranslocoMessageformat({ strictPluralKeys: false }),
    ],
  });
  const apply = vi.spyOn(TestBed.inject(Seo), 'apply');
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
  harness.fixture.detectChanges();
  return { apply, requested, harness, host: harness.routeNativeElement as HTMLElement };
}

function textsOf(host: Element, selector: string): string[] {
  return [...host.querySelectorAll(selector)].map(
    (element) => element.textContent?.replace(/\s+/g, ' ').trim() ?? '',
  );
}

function hrefsOf(host: Element, selector: string): (string | null)[] {
  return [...host.querySelectorAll(selector)].map((element) => element.getAttribute('href'));
}

describe('TrendsPage', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.head.querySelectorAll('[data-lodb-seo]').forEach((element) => element.remove());
  });

  it('ranks the builds with their scores, each linked to its shared page', async () => {
    const { host } = await visit(trendsPageOf());

    expect(textsOf(host, '[data-total]')).toEqual(['22 résultats']);
    expect(textsOf(host, '.vote-score')).toEqual(['+5', '-1']);
    expect(hrefsOf(host, '.trend-row__id')).toEqual([
      `/b/${'1'.padStart(24, 'a')}`,
      `/b/${'2'.padStart(24, 'a')}`,
    ]);
    expect(textsOf(host, '.trend-row__author')).toEqual(['Par Faker#KR1', 'Par Faker#KR1']);
    expect(host.querySelectorAll('.trend-row__author [role="img"]')).toHaveLength(2);
    expect(host.querySelectorAll('.trend-row__items .forge-ghost')).toHaveLength(2);
  });

  it('names languages in English, as the legacy did, and leaves out a missing patch', async () => {
    const [first, second] = trendsPageOf().rows;
    const unpinned = { ...second, gameVersion: '', language: 'fr_FR' };
    const { host } = await visit(trendsPageOf({ rows: [first, unpinned] }));

    const chips = [...host.querySelectorAll('.trend-row__chips')].map((row) =>
      textsOf(row, 'span').slice(1),
    );
    expect(chips).toEqual([['16.19.1', 'English (United States)'], ['French']]);
  });

  it('offers the sign-up to a visitor, the editor to a signed-in reader', async () => {
    const { host } = await visit(trendsPageOf());
    expect(hrefsOf(host, 'lodb-forge-cta a')).toEqual(['/fr/account/register']);

    TestBed.resetTestingModule();
    const signedIn = await visit(trendsPageOf(), 'authenticated');
    expect(hrefsOf(signedIn.host, 'lodb-forge-cta a')).toEqual(['/fr/account/builds/new']);
  });

  it('pages through the query, keeping its filters', async () => {
    const { host } = await visit(trendsPageOf(), 'unknown', '/fr/trends?mode=aram');

    expect(textsOf(host, '.trends-pager__position')).toEqual(['1 / 2']);
    expect(hrefsOf(host, '.trends-pager a')).toEqual(['/fr/trends?mode=aram&page=2']);
    const next = host.querySelector('.trends-pager a');
    expect(next?.getAttribute('rel')).toBe('next');
    expect(next?.getAttribute('aria-label')).toBe('Suivant');
    expect(host.querySelector('.trends-pager__nav--off')?.getAttribute('aria-hidden')).toBe('true');
  });

  it('links the first page bare from the second', async () => {
    const { host } = await visit(trendsPageOf({ page: 2 }), 'unknown', '/fr/trends?page=2');

    expect(hrefsOf(host, '.trends-pager a[rel="prev"]')).toEqual(['/fr/trends']);
    expect(host.querySelector('.trends-pager a[rel="next"]')).toBeNull();
  });

  it('selects the filters applied, submits to the trends without scripts, and keeps the names context', async () => {
    const filters = { champion: 'MonkeyKing', mode: 'aram' as const, language: null };
    const { host } = await visit(trendsPageOf({ filters }), 'unknown', '/fr/trends?lang=fr_FR');
    const form = host.querySelector('form') as HTMLFormElement;
    const select = (name: string) =>
      form.querySelector<HTMLSelectElement>(`select[name="${name}"]`);

    expect(form.getAttribute('action')).toBe('/fr/trends');
    expect(form.getAttribute('method')).toBe('get');
    expect(select('champion')?.value).toBe('MonkeyKing');
    expect(select('mode')?.value).toBe('aram');
    expect(select('language')?.value).toBe('');
    expect(form.querySelector<HTMLInputElement>('input[name="lang"]')?.value).toBe('fr_FR');
  });

  // The server's HTML carries attributes only: a `selected` property alone would leave every
  // filter on "all" in a page read without scripts.
  it('marks the filters applied as selected in the markup, for a page without scripts', async () => {
    const filters = { champion: 'MonkeyKing', mode: 'aram' as const, language: 'fr_FR' };
    const { host } = await visit(trendsPageOf({ filters }), 'unknown', '/fr/trends?mode=aram');
    const selectedIn = (name: string) =>
      [...host.querySelectorAll(`select[name="${name}"] option[selected]`)].map((option) =>
        option.getAttribute('value'),
      );

    expect(selectedIn('champion')).toEqual(['MonkeyKing']);
    expect(selectedIn('mode')).toEqual(['aram']);
    expect(selectedIn('language')).toEqual(['fr_FR']);
  });

  it('navigates to the filters chosen, back to the first page', async () => {
    const { host, harness } = await visit(
      trendsPageOf({ page: 2 }),
      'unknown',
      '/fr/trends?page=2&lang=fr_FR',
    );
    const form = host.querySelector('form') as HTMLFormElement;
    const champion = form.querySelector<HTMLSelectElement>('select[name="champion"]');
    if (champion) champion.value = 'Ahri';

    form.dispatchEvent(new SubmitEvent('submit', { cancelable: true }));
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/fr/trends?lang=fr_FR&champion=Ahri');
  });

  it('invites to forge a build when none matches', async () => {
    const { host } = await visit(trendsPageOf({ rows: [], total: 0, pages: 1 }));

    expect(textsOf(host, '[data-empty] p')).toContain('Aucun build public.');
    expect(host.querySelector('lodb-trend-row')).toBeNull();
    expect(host.querySelector('.trends-pager')).toBeNull();
  });

  it('reads the page again once signed in, for the reader’s own votes', async () => {
    const { host, requested } = await visit(trendsPageOf(), 'authenticated');

    expect(requested).toEqual(['/api/trends?lang=fr_FR']);
    expect(textsOf(host, '.vote-score')).toEqual(['+6', '0']);
    expect(host.querySelectorAll('.vote-arrow--on-up')).toHaveLength(2);
  });

  it('writes the indexable head of the trends, in its locale', async () => {
    const { apply, requested } = await visit(trendsPageOf());

    expect(requested).toEqual([]);
    expect(apply).toHaveBeenLastCalledWith(
      expect.objectContaining({ path: 'trends', locale: 'fr' }),
    );
    expect(document.head.querySelector('link[rel="canonical"]')?.getAttribute('href')).toBe(
      `${ORIGIN}/fr/trends`,
    );
    // The site's own graph comes first, on every page; the trends add theirs.
    const types = [...document.head.querySelectorAll('script[type="application/ld+json"]')]
      .map((script) => (JSON.parse(script.textContent ?? '{}') as { '@type'?: string })['@type'])
      .filter((type) => type !== undefined);
    expect(types).toEqual(['BreadcrumbList', 'ItemList']);
  });
});
