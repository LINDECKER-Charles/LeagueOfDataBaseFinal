import { PlatformLocation } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { type Translation, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { provideI18n } from './provide-i18n';

const RESULTS_FR = '{count, plural, =1 {# résultat} other {# résultats}}';
const RESULTS_EN = '{count, plural, =1 {# result} other {# results}}';

// Shaped like the built catalogues (npm run i18n:build): every locale completed with the `en`
// messages it lacks, `{{ }}` parameters.
const CATALOGUES: Record<string, Translation> = {
  'i18n/fr.json': { filter: { page: 'page {{ page }} sur {{ count }}', results: RESULTS_FR } },
  'i18n/ja.json': { filter: { page: '{{ page }} / {{ count }} ページ', results: RESULTS_EN } },
  'i18n/seo/fr.json': { title: 'Champions FR', suffix: '(patch {{ version }})' },
};

describe('provideI18n', () => {
  let transloco: TranslocoService;
  let requested: string[];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideI18n()],
    });
    transloco = TestBed.inject(TranslocoService);
    requested = [];
  });

  async function load(path: string): Promise<void> {
    const loaded = firstValueFrom(transloco.load(path));
    const http = TestBed.inject(HttpTestingController);
    for (const request of http.match(() => true)) {
      requested.push(request.request.url);
      request.flush(CATALOGUES[request.request.url]);
    }
    await loaded;
    http.verify();
  }

  const counts = () => [0, 1, 2].map((count) => transloco.translate('filter.results', { count }));

  it('loads the catalogue of the locale alone, never en beside it', async () => {
    await load('fr');
    await load('seo/fr');
    transloco.setActiveLang('fr');

    expect(requested).toEqual(['i18n/fr.json', 'i18n/seo/fr.json']);
    expect(transloco.translate('filter.page', { page: 2, count: 5 })).toBe('page 2 sur 5');
    expect(transloco.translate('seo.suffix', { version: '16.1' })).toBe('(patch 16.1)');
  });

  // The legacy lists took their singular for a count of exactly one, whatever the locale:
  // French reads "0 résultats", and the `en` message of Japanese, which has no `one`
  // category, still reads "1 result".
  it.each([
    ['fr', ['0 résultats', '1 résultat', '2 résultats']],
    ['ja', ['0 results', '1 result', '2 results']],
  ])('renders the counts of %s with the singular for one exactly', async (locale, results) => {
    await load(locale);
    transloco.setActiveLang(locale);

    expect(counts()).toEqual(results);
  });

  it('formats the parameters of an en message under a locale of its own', async () => {
    await load('ja');
    transloco.setActiveLang('ja');

    expect(transloco.translate('filter.page', { page: 1, count: 3 })).toBe('1 / 3 ページ');
  });
});

describe('provideI18n before the first render', () => {
  function start(pathname: string): TranslocoService {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideI18n(),
        { provide: PlatformLocation, useValue: { pathname } },
      ],
    });
    return TestBed.inject(TranslocoService);
  }

  // No request may leave before the platform is detected, another initializer.
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('activates the locale of the URL, so the chrome renders in it', () => {
    expect(start('/fr/items').getActiveLang()).toBe('fr');
  });

  it.each(['/', '/admin/users', '/b/0123456789abcdef01234567', '/EN/items'])(
    'leaves %s, outside the locales, to the default one until its route decides',
    (pathname) => {
      expect(start(pathname).getActiveLang()).toBe('en');
    },
  );
});
