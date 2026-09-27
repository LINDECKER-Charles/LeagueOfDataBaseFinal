import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import { ApiMeta } from '../../api/meta/api-meta';
import { BottomNav } from '../bottom-nav/bottom-nav';
import { Footer } from '../footer/footer';
import { Header } from '../header/header';

const META: CatalogMeta = {
  latest: '16.19.1',
  versions: ['16.19.1', '14.1.1'],
  readyVersions: ['16.19.1'],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US', 'en_GB'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [{ locale: 'en', language: 'en_US' }],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};

@Component({
  imports: [BottomNav, Footer, Header],
  template: '<lodb-header /><lodb-footer /><lodb-bottom-nav />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Chrome {}

async function chromeAt(url: string): Promise<HTMLElement> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: '**', component: Chrome }]),
      provideTransloco({
        config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
        loader: class {
          getTranslation = () => of({});
        },
      }),
      { provide: ApiMeta, useValue: { meta: () => of(META) } },
    ],
  });
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
  harness.detectChanges();
  return harness.routeNativeElement as HTMLElement;
}

function hrefs(host: HTMLElement, selector: string): (string | null)[] {
  return Array.from(host.querySelectorAll(selector), (link) => link.getAttribute('href'));
}

describe('the chrome on a pinned page', () => {
  it('keeps the version and the variant in every catalogue link, and only there', async () => {
    const host = await chromeAt('/en/14.1.1/champions/Ahri?lang=en_GB');

    expect(hrefs(host, '.bottom-nav a')).toEqual([
      '/en',
      '/en/14.1.1/champions?lang=en_GB',
      '/en/14.1.1/items?lang=en_GB',
      '/en/14.1.1/runes?lang=en_GB',
      '/en/14.1.1/summoners?lang=en_GB',
      '/en/trends',
    ]);
    expect(hrefs(host, '.switcher--nav .switcher__panel a')).toEqual(
      hrefs(host, '.bottom-nav a').slice(1, 5),
    );
    expect(hrefs(host, 'footer nav a')).toContain('/en/14.1.1/runes?lang=en_GB');
    expect(hrefs(host, 'footer nav a')).toContain('/en/about');
  });

  it('lights the Encyclopedia menu and the champions tab', async () => {
    const host = await chromeAt('/en/14.1.1/champions/Ahri');

    expect(host.querySelector('.switcher--nav')?.classList).toContain('switcher--current');
    const current = host.querySelectorAll('.bottom-nav a[aria-current="page"]');
    expect(Array.from(current, (link) => link.getAttribute('href'))).toEqual([
      '/en/14.1.1/champions',
    ]);
  });

  it('follows the latest version elsewhere', async () => {
    const host = await chromeAt('/en/trends');

    expect(hrefs(host, '.bottom-nav a')[1]).toBe('/en/champions');
  });
});
