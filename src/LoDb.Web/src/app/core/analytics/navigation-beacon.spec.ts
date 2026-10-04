import { provideLocationMocks } from '@angular/common/testing';
import { ApplicationInitStatus, Component, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { PLATFORM } from '../platform/platform';
import type { PlatformKind } from '../platform/platform-kind';
import { provideNavigationBeacon } from './provide-navigation-beacon';

@Component({ template: '' })
class Probe {}

const ORIGIN = 'https://league-of-data-base.test';
const VIEW_URL = `${ORIGIN}/api/analytics/view`;

describe('NavigationBeacon', () => {
  const navigator = window.navigator as unknown as Record<string, unknown>;
  let sendBeacon: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    sendBeacon = vi.fn(() => true);
    Object.defineProperty(navigator, 'sendBeacon', { value: sendBeacon, configurable: true });
  });

  afterEach(() => {
    delete navigator['sendBeacon'];
  });

  async function started(kind: PlatformKind = 'web', platformId = 'browser'): Promise<Router> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', component: Probe }]),
        provideLocationMocks(),
        provideNavigationBeacon(),
        { provide: PLATFORM_ID, useValue: platformId },
        { provide: PLATFORM, useValue: { kind, apiOrigin: () => ORIGIN } },
      ],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    return TestBed.inject(Router);
  }

  function sentPaths(): string[] {
    return sendBeacon.mock.calls.map(([url, body]) => {
      expect(url).toBe(VIEW_URL);
      return (JSON.parse(body as string) as { path: string }).path;
    });
  }

  it('leaves the first navigation to the mirror and counts the next ones', async () => {
    const router = await started();

    await router.navigateByUrl('/fr/champions');
    await router.navigateByUrl('/fr/champions/Ahri');
    await router.navigateByUrl('/fr/items?lang=fr_FR#top');

    expect(sentPaths()).toEqual(['/fr/champions/Ahri', '/fr/items?lang=fr_FR']);
  });

  it('counts a change of language or version, not of a filter', async () => {
    const router = await started();

    await router.navigateByUrl('/en/items');
    await router.navigateByUrl('/en/items?q=boots');
    await router.navigateByUrl('/en/items?q=boots&page=2');
    await router.navigateByUrl('/en/items?q=boots&lang=en_GB');
    await router.navigateByUrl('/en/items?lang=en_GB&version=16.1.1');

    expect(sentPaths()).toEqual([
      '/en/items?q=boots&lang=en_GB',
      '/en/items?lang=en_GB&version=16.1.1',
    ]);
  });

  it('posts the home of a locale with its trailing slash', async () => {
    const router = await started();

    await router.navigateByUrl('/fr/items');
    await router.navigateByUrl('/fr');

    expect(sentPaths()).toEqual(['/fr/']);
  });

  it('stays silent in the desktop and Android shells', async () => {
    const router = await started('android');

    await router.navigateByUrl('/fr/items');
    await router.navigateByUrl('/fr/runes');

    expect(sendBeacon).not.toHaveBeenCalled();
  });

  it('stays silent while rendering on the server', async () => {
    const router = await started('web', 'server');

    await router.navigateByUrl('/fr/items');
    await router.navigateByUrl('/fr/runes');

    expect(sendBeacon).not.toHaveBeenCalled();
  });

  it('does nothing in a browser without sendBeacon', async () => {
    delete navigator['sendBeacon'];
    const router = await started();

    await router.navigateByUrl('/fr/items');

    await expect(router.navigateByUrl('/fr/runes')).resolves.toBe(true);
  });
});
