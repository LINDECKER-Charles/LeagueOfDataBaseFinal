import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, type Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import { activateLocale } from '../../../core/i18n/activate-locale';
import { LOCALES } from '../../../core/i18n/locales';
import { CANONICAL_ORIGIN } from '../../../core/seo/canonical-origin';
import { Seo } from '../../../core/seo/seo';
import { EDITORIAL_ROUTES } from '../editorial.routes';
import { LEGAL_CONTENTS } from './legal-contents';
import { legalLanguageOf } from './legal-language-of';
import type { LegalPageId } from './legal-page-id';

const CATALOGUES: Record<string, Translation> = {
  en: { legal: { notice: { title: 'Legal notice' } } },
  de: { legal: { notice: { title: 'Impressum' } } },
  'seo/en': { legal: { notice: { description: 'Who publishes the site.' } } },
  'seo/de': { legal: { notice: { description: 'Wer die Seite herausgibt.' } } },
};
const PAGES: LegalPageId[] = ['notice', 'privacy', 'terms', 'cookies'];

// The section nav follows the reading through an observer jsdom does not have.
class NoIntersectionObserver {
  observe = vi.fn();
  unobserve = vi.fn();
  disconnect = vi.fn();
}

async function visit(url: string) {
  TestBed.configureTestingModule({
    providers: [
      { provide: CANONICAL_ORIGIN, useValue: 'https://league-of-data-base.com' },
      provideRouter([
        { path: ':locale', resolve: { locale: activateLocale }, children: EDITORIAL_ROUTES },
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

function textOf(page: HTMLElement): HTMLElement {
  return page.querySelector<HTMLElement>('.hx-prose')?.parentElement ?? page;
}

describe('legalLanguageOf', () => {
  it.each([
    ['fr', 'fr'],
    ['en', 'en'],
    ['de', 'en'],
    ['ar', 'en'],
    ['zh-hant', 'en'],
  ] as const)('writes the legal texts of %s in %s', (locale, language) => {
    expect(legalLanguageOf(locale)).toBe(language);
  });

  it('has a text for every site locale', () => {
    for (const locale of LOCALES) {
      expect(['fr', 'en']).toContain(legalLanguageOf(locale));
    }
  });
});

describe('LegalPage', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
    vi.stubGlobal('IntersectionObserver', NoIntersectionObserver);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
    document.head.querySelectorAll('[data-lodb-seo]').forEach((element) => element.remove());
  });

  it('shows the French text under fr, in the page language', async () => {
    const { page } = await visit('/fr/legal/notice');

    expect(page.querySelector('#editeur h2')?.textContent).toContain('Éditeur du site');
    expect(page.querySelector('#publisher')).toBeNull();
    expect(textOf(page).hasAttribute('lang')).toBe(false);
  });

  it('shows the English text under another locale, marked as English', async () => {
    const { page } = await visit('/de/legal/notice');

    expect(page.querySelector('#publisher h2')?.textContent).toContain('Site publisher');
    expect(page.querySelector('#editeur')).toBeNull();
    expect(textOf(page).getAttribute('lang')).toBe('en');
  });

  it('writes the head in the page locale, whatever the text language', async () => {
    const { page, apply } = await visit('/de/legal/notice');

    expect(page.querySelector('h1')?.textContent?.trim()).toBe('Impressum');
    expect(apply).toHaveBeenLastCalledWith({
      title: 'Impressum',
      description: 'Wer die Seite herausgibt.',
      path: 'legal/notice',
    });
  });

  it.each(
    PAGES.flatMap((id) => [
      { id, locale: 'fr' as const },
      { id, locale: 'en' as const },
    ]),
  )('lists the sections of the $id text in $locale, in order', async ({ id, locale }) => {
    const { page } = await visit(`/${locale}/legal/${id}`);
    const sections = [...page.querySelectorAll('.hx-prose section[id]')].map(
      (section) => section.id,
    );
    const links = [...page.querySelectorAll('.section-nav a')].map((link) =>
      link.getAttribute('href'),
    );

    expect(sections).toEqual(LEGAL_CONTENTS[id][locale].map((entry) => entry.id));
    expect(links).toEqual(sections.map((section) => `/${locale}/legal/${id}#${section}`));
  });

  it('links the other legal pages within the locale', async () => {
    const { page } = await visit('/de/legal/notice');
    const hrefs = [...page.querySelectorAll('#data a')].map((link) => link.getAttribute('href'));

    expect(hrefs).toEqual(['/de/legal/privacy', '/de/legal/cookies', '/de/legal/terms']);
  });
});
