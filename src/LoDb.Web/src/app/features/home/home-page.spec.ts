import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, type Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import { CANONICAL_ORIGIN } from '../../core/seo/canonical-origin';
import { Seo } from '../../core/seo/seo';
import { HomePage } from './home-page';

const ORIGIN = 'https://league-of-data-base.com';
const CATALOGUES: Record<string, Translation> = {
  en: { homepage: { title: 'League of Data Base' } },
  fr: { homepage: { title: 'La base de LoL' } },
};

// One route for every locale, as app.routes.ts mounts the home: the page is then reused.
async function visit(url: string) {
  TestBed.configureTestingModule({
    providers: [
      { provide: CANONICAL_ORIGIN, useValue: ORIGIN },
      provideRouter([{ path: ':locale', component: HomePage }]),
      provideTransloco({
        config: {
          availableLangs: ['en', 'fr'],
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

describe('HomePage', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
    headElements('').forEach((element) => element.remove());
  });

  it('titles the document with its heading alone', async () => {
    document.documentElement.lang = 'fr';

    const { apply } = await visit('/fr');

    expect(apply).toHaveBeenLastCalledWith({
      title: 'La base de LoL',
      path: '',
      titleFormat: 'raw',
      locale: 'fr',
    });
    expect(document.title).toBe('La base de LoL');
  });

  it('is indexable, canonical at the root of its locale, with 21 alternates and x-default', async () => {
    document.documentElement.lang = 'fr';

    await visit('/fr');
    const hreflangs = headElements('link[rel="alternate"]').map((link) =>
      link.getAttribute('hreflang'),
    );

    expect(headElements('link[rel="canonical"]')).toHaveLength(1);
    expect(hrefOf('link[rel="canonical"]')).toBe(`${ORIGIN}/fr/`);
    expect(headElements('meta[name="robots"]')[0]?.getAttribute('content')).toBe('index, follow');
    expect(new Set(hreflangs).size).toBe(22);
    expect(hreflangs.at(-1)).toBe('x-default');
    expect(hrefOf('link[rel="alternate"][hreflang="x-default"]')).toBe(`${ORIGIN}/`);
  });

  it('rewrites its head when the router reuses it for another locale', async () => {
    document.documentElement.lang = 'fr';
    const { harness, apply } = await visit('/fr');

    document.documentElement.lang = 'en';
    await harness.navigateByUrl('/en');
    await harness.fixture.whenStable();

    expect(apply).toHaveBeenLastCalledWith(expect.objectContaining({ locale: 'en' }));
    expect(document.title).toBe('League of Data Base');
    expect(hrefOf('link[rel="canonical"]')).toBe(`${ORIGIN}/en/`);
  });
});
