import { provideLocationMocks } from '@angular/common/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withRouterConfig } from '@angular/router';
import { AUDIENCE_TEST_ROUTES } from '../testing/audience-test-routes';
import { audiencePageOf } from './audience-page-of';
import { audienceReferrerOf } from './audience-referrer-of';

async function pageAt(url: string) {
  const router = TestBed.inject(Router);
  await router.navigateByUrl(url);
  return audiencePageOf(router.routerState.snapshot.root, router.url);
}

describe('audiencePageOf', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          AUDIENCE_TEST_ROUTES,
          withRouterConfig({ paramsInheritanceStrategy: 'always' }),
        ),
        provideLocationMocks(),
      ],
    });
  });

  it.each([
    ['/fr/champions', '/fr/champions'],
    ['/fr/champions/103-ahri#lore', '/fr/champions/103-ahri'],
    ['/fr/champions?q=ahri&tags=Mage&page=2&lang=fr_FR', '/fr/champions?lang=fr_FR'],
    ['/en/champions?version=16.1.1&lang=en_GB', '/en/champions?lang=en_GB&version=16.1.1'],
    ['/fr/champions?lang=%3Cscript%3E&version=a%20b', '/fr/champions'],
    ['/fr/16.1.1/items/1036-long-sword', '/fr/16.1.1/items/1036-long-sword'],
  ])('sends the real path of a list or a detail: %s', async (url, sent) => {
    const page = await pageAt(url);

    expect(page?.url).toBe(sent);
    expect(page?.isPublicPath).toBe(true);
  });

  it.each([
    ['/fr/u/Faker?tab=builds', '/fr/u/:username'],
    ['/b/Zx81bQ2token', '/b/:token'],
    ['/fr/legal/privacy?from=footer#audience', '/fr/legal/privacy'],
    ['/fr?version=16.1.1', '/fr/'],
  ])('sends the template of any other page: %s', async (url, sent) => {
    const page = await pageAt(url);

    expect(page?.url).toBe(sent);
    expect(page?.isPublicPath).toBe(false);
  });

  it.each(['/fr/account/reset-password/a-secret-token?user=42', '/admin/users'])(
    'sends nothing of an account or admin page: %s',
    async (url) => {
      expect(await pageAt(url)).toBeNull();
    },
  );

  it('sends nothing of a redirect, which the browser follows instead', async () => {
    expect(await pageAt('/fr/old-items')).toBeNull();
  });

  it('turns an outcome rendered in place into an error of its template', async () => {
    const page = await pageAt('/fr/no/such/page?id=secret');

    expect(page).toMatchObject({ url: '/fr/**', errorStatus: 404, isPublicPath: false });
  });

  it('names the locale and the version, current or pinned', async () => {
    expect(await pageAt('/fr/champions/103-ahri')).toMatchObject({
      locale: 'fr',
      version: 'current',
    });
    expect(await pageAt('/en/16.1.1/items/1036-long-sword')).toMatchObject({
      locale: 'en',
      version: '16.1.1',
    });
    expect(await pageAt('/b/Zx81bQ2token')).toMatchObject({ locale: null });
  });

  it('tells a list search without keeping its text', async () => {
    const page = await pageAt('/fr/champions?q=my%20secret');

    expect(page?.isSearching).toBe(true);
    expect(page?.url).not.toContain('secret');
    expect((await pageAt('/fr/champions?q='))?.isSearching).toBe(false);
  });

  it('keys a page by its path, language and version, like the dedup of the views', async () => {
    const plain = await pageAt('/fr/champions');
    const filtered = await pageAt('/fr/champions?tags=Mage&q=a');
    const variant = await pageAt('/fr/champions?lang=fr_FR');

    expect(filtered?.key).toBe(plain?.key);
    expect(variant?.key).not.toBe(plain?.key);
  });
});

describe('audienceReferrerOf', () => {
  it.each([
    ['https://www.google.com/search?q=league+data+base', 'https://www.google.com'],
    ['http://forum.example.org:8080/thread/9#reply', 'http://forum.example.org:8080'],
    ['https://league-of-data-base.com/fr/items', ''],
    ['android-app://com.google.android.gm/', ''],
    ['', ''],
    ['not a url', ''],
  ])('reduces %s to %s', (referrer, origin) => {
    expect(audienceReferrerOf(referrer, 'league-of-data-base.com')).toBe(origin);
  });
});
