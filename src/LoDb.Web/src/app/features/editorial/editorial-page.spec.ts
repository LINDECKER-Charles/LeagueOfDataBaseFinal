import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, type Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import { CANONICAL_ORIGIN } from '../../core/seo/canonical-origin';
import { Seo } from '../../core/seo/seo';
import { EDITORIAL_ROUTES } from './editorial.routes';

const ORIGIN = 'https://league-of-data-base.com';
const CATALOGUES: Record<string, Translation> = {
  en: { legal: { terms: { title: 'Terms of use' } } },
  de: { legal: { terms: { title: 'Nutzungsbedingungen' } } },
  'about/en': { data: { title: 'Where the data comes from' } },
  'about/de': { data: { title: 'Woher die Daten stammen' } },
};

// The editorial routes, mounted below the locale as app.routes.ts does.
async function visit(url: string) {
  TestBed.configureTestingModule({
    providers: [
      { provide: CANONICAL_ORIGIN, useValue: ORIGIN },
      provideRouter([{ path: ':locale', children: EDITORIAL_ROUTES }]),
      provideTransloco({
        config: {
          availableLangs: ['en', 'de'],
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
  return { harness, apply };
}

function headElements(selector: string): Element[] {
  return [...document.head.querySelectorAll(`[data-lodb-seo]${selector}`)];
}

function hrefOf(selector: string): string | null | undefined {
  return headElements(selector)[0]?.getAttribute('href');
}

describe('EditorialPage', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
    headElements('').forEach((element) => element.remove());
  });

  it('titles a page of the about scope and makes its own path canonical', async () => {
    document.documentElement.lang = 'de';

    const { apply } = await visit('/de/about/data');

    expect(apply).toHaveBeenLastCalledWith({
      title: 'Woher die Daten stammen',
      path: 'about/data',
      locale: 'de',
    });
    expect(document.title).toBe('Woher die Daten stammen — League Of Data Base');
    expect(headElements('link[rel="canonical"]')).toHaveLength(1);
    expect(hrefOf('link[rel="canonical"]')).toBe(`${ORIGIN}/de/about/data`);
  });

  it('titles a page of the root catalogue', async () => {
    document.documentElement.lang = 'en';

    const { apply } = await visit('/en/legal/terms');

    expect(apply).toHaveBeenLastCalledWith({
      title: 'Terms of use',
      path: 'legal/terms',
      locale: 'en',
    });
  });

  it('is indexable, with 21 alternates and an English x-default', async () => {
    document.documentElement.lang = 'de';

    await visit('/de/about/data');
    const hreflangs = headElements('link[rel="alternate"]').map((link) =>
      link.getAttribute('hreflang'),
    );

    expect(headElements('meta[name="robots"]')[0]?.getAttribute('content')).toBe('index, follow');
    expect(new Set(hreflangs).size).toBe(22);
    expect(hrefOf('link[rel="alternate"][hreflang="x-default"]')).toBe(`${ORIGIN}/en/about/data`);
  });
});
