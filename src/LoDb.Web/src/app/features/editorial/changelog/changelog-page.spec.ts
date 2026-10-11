import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { activateLocale } from '../../../core/i18n/activate-locale';
import { LOCALES } from '../../../core/i18n/locales';
import { CANONICAL_ORIGIN } from '../../../core/seo/canonical-origin';
import { EDITORIAL_ROUTES } from '../editorial.routes';
import { ChangelogReader } from './changelog-reader';
import type { ChangelogRelease } from './model/changelog-release';

function release(version: string, codename: string): ChangelogRelease {
  return {
    version,
    codename,
    date: '2026-08-22',
    type: 'minor',
    summary: `${codename}, en bref`,
    intro: `${codename}, en détail`,
    features: [],
    fixes: [{ id: null, area: null, text: 'Un correctif' }],
    devNote: null,
  };
}

// As the reader hands them: in the manifest's order, newest first.
const RELEASES = [release('2.2.1', 'Tamis'), release('2.2.0', 'Jade'), release('2.1.0', 'Héraut')];

async function visit(url: string): Promise<HTMLElement> {
  TestBed.configureTestingModule({
    providers: [
      { provide: CANONICAL_ORIGIN, useValue: 'https://league-of-data-base.com' },
      {
        provide: ChangelogReader,
        useValue: { releases: () => of(RELEASES), latestVersion: () => of('2.2.1') },
      },
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
          getTranslation = () => of({});
        },
      }),
    ],
  });
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
  return harness.routeNativeElement as HTMLElement;
}

function releasesOf(page: HTMLElement): HTMLElement[] {
  return [...page.querySelectorAll<HTMLElement>('.timeline > li')];
}

describe('ChangelogPage', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
    document.head.querySelectorAll('[data-lodb-seo]').forEach((element) => element.remove());
  });

  it('lists the releases newest first, the newest one open', async () => {
    const releases = releasesOf(await visit('/en/changelog'));

    expect(releases.map((node) => node.querySelector('.release__version')?.textContent)).toEqual([
      '2.2.1',
      '2.2.0',
      '2.1.0',
    ]);
    expect(releases.map((node) => node.querySelector('details')?.open)).toEqual([
      true,
      false,
      false,
    ]);
  });

  it('anchors each release on its version, as the former site did', async () => {
    const releases = releasesOf(await visit('/en/changelog'));

    expect(releases.map((node) => node.id)).toEqual(['v2-2-1', 'v2-2-0', 'v2-1-0']);
  });

  it('marks the French notes of a release read under another locale', async () => {
    const [newest] = releasesOf(await visit('/de/changelog'));

    for (const notes of [
      newest?.querySelector('.release__lede'),
      newest?.querySelector('.release__body'),
    ]) {
      expect(notes?.getAttribute('lang')).toBe('fr');
      expect(notes?.getAttribute('dir')).toBe('ltr');
    }
    // The label of the fixes is the page's own, inside notes read left to right.
    expect(newest?.querySelector('.fixes__label')?.getAttribute('lang')).toBe('de');
  });

  it('leaves the notes unmarked under fr, the language they are written in', async () => {
    const [newest] = releasesOf(await visit('/fr/changelog'));

    expect(newest?.querySelector('.release__body')?.hasAttribute('lang')).toBe(false);
    expect(newest?.querySelector('.fixes__label')?.hasAttribute('lang')).toBe(false);
  });
});
