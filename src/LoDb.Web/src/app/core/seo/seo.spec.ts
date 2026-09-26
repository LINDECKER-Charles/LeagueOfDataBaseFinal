import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  provideTransloco,
  type Translation,
  type TranslocoLoader,
  TranslocoService,
} from '@jsverse/transloco';
import { type Observable, delay, of, throwError } from 'rxjs';
import { LOCALES } from '../i18n/locales';
import { CANONICAL_ORIGIN } from './canonical-origin';
import { breadcrumbList } from './json-ld/site/breadcrumb-list';
import { Seo } from './seo';
import type { SeoPage } from './seo-page';

const ORIGIN = 'https://league-of-data-base.com';
const CATALOGUES: Record<string, Translation> = {
  en: { base: { title: 'League of Data Base', description: 'The LoL encyclopedia.' } },
  fr: { base: { title: 'League of Data Base', description: "L'encyclopédie de LoL." } },
  'seo/en': { versioned_suffix: '(patch {{ version }})' },
  'seo/fr': { versioned_suffix: '(version {{ version }})' },
};

// Asynchronous like the HTTP loader, so a render has to wait for the texts.
class InMemoryLoader implements TranslocoLoader {
  getTranslation(path: string): Observable<Translation> {
    const catalogue = CATALOGUES[path];
    return catalogue
      ? of(catalogue).pipe(delay(1))
      : throwError(() => new Error(`No catalogue for ${path}`));
  }
}

const AHRI: SeoPage = {
  title: 'Ahri, champion de LoL',
  description: 'Ahri dans League of Legends.',
  locale: 'fr',
  path: 'champions/Ahri',
  image: 'https://ddragon.example/Ahri_0.jpg',
  jsonLd: (urls) => [breadcrumbList([{ name: 'Ahri', url: urls.canonical }])],
};

function seo(): Seo {
  TestBed.configureTestingModule({
    providers: [
      { provide: CANONICAL_ORIGIN, useValue: ORIGIN },
      provideTransloco({
        config: {
          availableLangs: [...LOCALES],
          defaultLang: 'en',
          fallbackLang: 'en',
          failedRetries: 0,
          missingHandler: { useFallbackTranslation: true, logMissingKey: false },
          prodMode: true,
        },
        loader: InMemoryLoader,
      }),
    ],
  });
  return TestBed.inject(Seo);
}

function managed(selector: string): Element[] {
  return [...document.head.querySelectorAll(`[data-lodb-seo]${selector}`)];
}

function meta(key: string): string | null | undefined {
  return managed(`meta[name="${key}"], meta[property="${key}"]`)[0]?.getAttribute('content');
}

function hrefs(rel: string): (string | null)[] {
  return managed(`link[rel="${rel}"]`).map((link) => link.getAttribute('href'));
}

function jsonLd(): Record<string, unknown>[] {
  return managed('script[type="application/ld+json"]').map((script) =>
    JSON.parse(script.textContent ?? ''),
  );
}

describe('Seo', () => {
  afterEach(() => {
    managed('').forEach((element) => element.remove());
    document.title = '';
  });

  describe('on an indexable page', () => {
    it('titles the page after the site and describes it', async () => {
      await seo().apply(AHRI);

      expect(document.title).toBe('Ahri, champion de LoL — League Of Data Base');
      expect(meta('description')).toBe('Ahri dans League of Legends.');
      expect(meta('robots')).toBe('index, follow');
    });

    it('is canonical under its locale, without the query of the current URL', async () => {
      await seo().apply(AHRI);

      expect(hrefs('canonical')).toEqual([`${ORIGIN}/fr/champions/Ahri`]);
      expect(meta('og:url')).toBe(`${ORIGIN}/fr/champions/Ahri`);
    });

    it('names its 21 locales and its en version as the default', async () => {
      await seo().apply(AHRI);
      const alternates = managed('link[rel="alternate"]');

      expect(alternates).toHaveLength(22);
      expect(alternates.map((link) => link.getAttribute('hreflang'))).toContain('zh-Hans');
      expect(alternates.at(-1)?.getAttribute('hreflang')).toBe('x-default');
      expect(alternates.at(-1)?.getAttribute('href')).toBe(`${ORIGIN}/en/champions/Ahri`);
    });

    it('shares with its own image, its locale and the site identity', async () => {
      await seo().apply(AHRI);

      expect(meta('og:image')).toBe('https://ddragon.example/Ahri_0.jpg');
      expect(meta('twitter:image')).toBe('https://ddragon.example/Ahri_0.jpg');
      expect(meta('og:locale')).toBe('fr_FR');
      expect(meta('og:type')).toBe('website');
      expect(meta('og:site_name')).toBe('League Of Data Base');
      expect(meta('twitter:card')).toBe('summary_large_image');
      expect(managed('meta[name="google-site-verification"]')).toHaveLength(2);
      expect(hrefs('sitemap')).toEqual(['/sitemap.xml']);
    });

    it('states the site graph, then its own nodes', async () => {
      await seo().apply(AHRI);
      const [graph, crumbs] = jsonLd();

      expect(jsonLd()).toHaveLength(2);
      expect(JSON.stringify(graph)).toContain(
        '"name":"Ahri, champion de LoL — League Of Data Base"',
      );
      expect(JSON.stringify(graph)).toContain('"inLanguage":"fr"');
      expect(crumbs['@type']).toBe('BreadcrumbList');
    });

    it('falls back to the site description and the home preview', async () => {
      await seo().apply({ title: 'Runes', locale: 'fr', path: 'runes' });

      expect(meta('description')).toBe("L'encyclopédie de LoL.");
      expect(meta('og:image')).toBe(`${ORIGIN}/preview/home.png`);
    });
  });

  it('keeps the home title as given and points its default to the root', async () => {
    await seo().apply({ title: 'League Of Data Base — LoL', titleFormat: 'raw', path: '' });

    expect(document.title).toBe('League Of Data Base — LoL');
    expect(hrefs('canonical')).toEqual([`${ORIGIN}/en/`]);
    expect(managed('link[hreflang="x-default"]')[0]?.getAttribute('href')).toBe(`${ORIGIN}/`);
  });

  it('makes a pinned version self-canonical, with its patch in the title', async () => {
    await seo().apply({ ...AHRI, version: '16.18.1' });

    expect(document.title).toBe('Ahri, champion de LoL (version 16.18.1) — League Of Data Base');
    expect(hrefs('canonical')).toEqual([`${ORIGIN}/fr/16.18.1/champions/Ahri`]);
    expect(managed('link[hreflang="x-default"]')[0]?.getAttribute('href')).toBe(
      `${ORIGIN}/en/16.18.1/champions/Ahri`,
    );
  });

  it('keeps a private page out of the index, titled like the account pages', async () => {
    await seo().apply({ title: 'Connexion', kind: 'private', locale: 'fr' });

    expect(document.title).toBe('Connexion · League of Data Base');
    expect(meta('robots')).toBe('noindex, nofollow');
    expect(hrefs('canonical')).toEqual([]);
    expect(hrefs('alternate')).toEqual([]);
    expect(meta('og:url')).toBeUndefined();
    expect(jsonLd()).toEqual([]);
  });

  it('titles the admin pages as the admin shell does', async () => {
    await seo().apply({ title: 'Audience', kind: 'private', titleFormat: 'admin' });

    expect(document.title).toBe('Audience · Admin · LODB');
  });

  it('keeps an error out of the index, without a canonical', async () => {
    await seo().apply({ title: 'Page introuvable', kind: 'error', locale: 'fr' });

    expect(document.title).toBe('Page introuvable — League Of Data Base');
    expect(meta('robots')).toBe('noindex, nofollow');
    expect(hrefs('canonical')).toEqual([]);
    expect(jsonLd()).toEqual([]);
  });

  it('keeps a shared build out of the index but previews it', async () => {
    await seo().apply({ title: 'Ahri mid', kind: 'share', image: '/cdn/blobs/ab12.png' });

    expect(meta('robots')).toBe('noindex, nofollow');
    expect(meta('og:image')).toBe(`${ORIGIN}/cdn/blobs/ab12.png`);
    expect(meta('og:title')).toBe('Ahri mid — League Of Data Base');
    expect(hrefs('canonical')).toEqual([]);
    expect(jsonLd()).toEqual([]);
  });

  it('keeps a donation return out of the index while following its links', async () => {
    await seo().apply({ title: 'Merci', kind: 'donation-return', locale: 'fr', path: 'donate' });

    expect(meta('robots')).toBe('noindex, follow');
    expect(hrefs('canonical')).toEqual([`${ORIGIN}/fr/donate`]);
    expect(hrefs('alternate')).toEqual([]);
  });

  it('replaces every tag of the previous page', async () => {
    const service = seo();
    await service.apply(AHRI);
    await service.apply({ title: 'Page introuvable', kind: 'error' });

    expect(managed('meta[name="robots"]')).toHaveLength(1);
    expect(hrefs('canonical')).toEqual([]);
    expect(hrefs('alternate')).toEqual([]);
    expect(jsonLd()).toEqual([]);
  });

  it('writes only the last of pages that follow each other quickly', async () => {
    const service = seo();

    await Promise.all([service.apply(AHRI), service.apply({ title: 'Items', path: 'items' })]);

    expect(document.title).toBe('Items — League Of Data Base');
    expect(hrefs('canonical')).toEqual([`${ORIGIN}/en/items`]);
  });

  it('uses the active locale when the page names none', async () => {
    const service = seo();
    TestBed.inject(TranslocoService).setActiveLang('fr');

    await service.apply({ title: 'Objets', path: 'items' });

    expect(hrefs('canonical')).toEqual([`${ORIGIN}/fr/items`]);
    expect(meta('og:locale')).toBe('fr_FR');
  });

  it('holds the application unstable until the head is written, as SSR needs', async () => {
    const service = seo();
    const application = TestBed.inject(ApplicationRef);

    void service.apply(AHRI);
    await application.whenStable();

    expect(hrefs('canonical')).toEqual([`${ORIGIN}/fr/champions/Ahri`]);
  });
});
