import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { type Translation, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { provideI18n } from './provide-i18n';

// Shaped like the converted catalogues (tools/next/i18n): `{{ }}` parameters, ICU plurals.
const CATALOGUES: Record<string, Translation> = {
  'i18n/en.json': {
    filter: {
      results: '{count, plural, one {# result} other {# results}}',
      page: 'page {{ page }} / {{ count }}',
    },
    only_in_en: 'Only in English',
  },
  'i18n/fr.json': {
    filter: {
      results: '{count, plural, one {# résultat} other {# résultats}}',
      page: 'page {{ page }} sur {{ count }}',
    },
  },
  'i18n/ja.json': { filter: { page: '{{ page }} / {{ count }} ページ' } },
  'i18n/seo/en.json': { title: 'Champions', suffix: '(patch {{ version }})' },
  'i18n/seo/fr.json': { title: 'Champions FR' },
};

describe('provideI18n', () => {
  let transloco: TranslocoService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideI18n()],
    });
    transloco = TestBed.inject(TranslocoService);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
  });

  afterEach(() => vi.restoreAllMocks());

  async function activate(path: string): Promise<void> {
    const loaded = firstValueFrom(transloco.load(path));
    const http = TestBed.inject(HttpTestingController);
    for (const request of http.match(() => true)) {
      request.flush(CATALOGUES[request.request.url]);
    }
    await loaded;
    http.verify();
  }

  it('falls back to en for a key the locale lacks, and only for that key', async () => {
    await activate('fr');
    transloco.setActiveLang('fr');

    expect(transloco.translate('filter.page', { page: 2, count: 5 })).toBe('page 2 sur 5');
    expect(transloco.translate('only_in_en')).toBe('Only in English');
  });

  it('falls back to en key by key inside a scope too', async () => {
    await activate('fr');
    await activate('seo/fr');
    transloco.setActiveLang('fr');

    expect(transloco.translate('seo.title')).toBe('Champions FR');
    expect(transloco.translate('seo.suffix', { version: '16.1' })).toBe('(patch 16.1)');
  });

  it('formats ICU plurals with the rules of the active locale', async () => {
    await activate('fr');
    transloco.setActiveLang('fr');

    const results = (count: number) => transloco.translate('filter.results', { count });
    expect([0, 1, 2].map(results)).toEqual(['0 résultat', '1 résultat', '2 résultats']);
  });

  it('renders an en plural under a locale without a `one` category', async () => {
    await activate('ja');
    transloco.setActiveLang('ja');

    expect(transloco.translate('filter.results', { count: 1 })).toBe('1 results');
    expect(transloco.translate('filter.page', { page: 1, count: 3 })).toBe('1 / 3 ページ');
  });
});
