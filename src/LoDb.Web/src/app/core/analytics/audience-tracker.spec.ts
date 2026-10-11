import { provideLocationMocks } from '@angular/common/testing';
import { ApplicationInitStatus, ApplicationRef, DOCUMENT, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withRouterConfig } from '@angular/router';
import { PLATFORM } from '../platform/platform';
import type { PlatformKind } from '../platform/platform-kind';
import { AudienceTracker } from './audience-tracker';
import { AudienceOptOut } from './audience/send/audience-opt-out';
import { AUDIENCE_TEST_ROUTES } from './audience/testing/audience-test-routes';
import {
  type FakeAudienceDocument,
  fakeAudienceDocument,
} from './audience/testing/fake-audience-document';
import { provideNavigationBeacon } from './provide-navigation-beacon';

const ORIGIN = 'https://league-of-data-base.com';
const SEND_URL = `${ORIGIN}/u/api/send`;
const WEBSITE = 'deee1958-0f6c-4a32-a327-aad389836c3b';

interface Sent {
  readonly url: string;
  readonly init: RequestInit;
  readonly body: { type: string; payload: Record<string, unknown> };
}

describe('AudienceTracker', () => {
  let fetchMock: ReturnType<typeof vi.fn>;
  let page: FakeAudienceDocument;

  beforeEach(() => {
    page = fakeAudienceDocument();
    let session = 0;
    fetchMock = vi.fn(() => {
      session += 1;
      return Promise.resolve(Response.json({ cache: `cache-${session}` }));
    });
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => vi.unstubAllGlobals());

  async function started(kind: PlatformKind = 'web', platformId = 'browser'): Promise<Router> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          AUDIENCE_TEST_ROUTES,
          withRouterConfig({ paramsInheritanceStrategy: 'always' }),
        ),
        provideLocationMocks(),
        provideNavigationBeacon(),
        { provide: DOCUMENT, useValue: page },
        { provide: PLATFORM_ID, useValue: platformId },
        { provide: PLATFORM, useValue: { kind, apiOrigin: () => ORIGIN } },
      ],
    });
    await TestBed.inject(ApplicationInitStatus).donePromise;
    return TestBed.inject(Router);
  }

  async function settle(): Promise<void> {
    await TestBed.inject(ApplicationRef).whenStable();
    await new Promise((resolve) => setTimeout(resolve));
  }

  async function visit(router: Router, url: string): Promise<void> {
    await router.navigateByUrl(url);
    await settle();
  }

  function sent(): Sent[] {
    return fetchMock.mock.calls.map(([url, init]) => ({
      url: url as string,
      init: init as RequestInit,
      body: JSON.parse((init as RequestInit).body as string) as Sent['body'],
    }));
  }

  const payloads = () => sent().map(({ body }) => body.payload);
  const sentUrls = () => payloads().map((payload) => payload['url']);
  const sentNames = () => payloads().map((payload) => payload['name'] ?? 'view');

  it('counts the first navigation, then each change of page, once', async () => {
    const router = await started();

    await visit(router, '/fr/champions');
    await visit(router, '/fr/champions?tags=Mage&page=2');
    await visit(router, '/fr/champions/103-ahri#lore');
    await visit(router, '/fr/champions/103-ahri?lang=fr_FR');
    await visit(router, '/fr/u/Faker');

    expect(sentUrls()).toEqual([
      '/fr/champions',
      '/fr/champions/103-ahri',
      '/fr/champions/103-ahri?lang=fr_FR',
      '/fr/u/:username',
    ]);
  });

  it('posts the send body of Umami, without credentials, outliving the page', async () => {
    page.referrer = 'https://www.google.com/search?q=ahri';
    page.title = 'Ahri - League Of Data Base';
    const router = await started();

    await visit(router, '/fr/champions/103-ahri');

    const [{ url, init, body }] = sent();
    expect(url).toBe(SEND_URL);
    expect(init).toMatchObject({ method: 'POST', credentials: 'omit', keepalive: true });
    expect(body).toEqual({
      type: 'event',
      payload: {
        website: WEBSITE,
        hostname: 'league-of-data-base.com',
        url: '/fr/champions/103-ahri',
        referrer: 'https://www.google.com',
        title: 'Ahri - League Of Data Base',
        language: 'fr-FR',
        screen: '1920x1080',
        tag: 'web',
        data: { locale: 'fr', version: 'current' },
      },
    });
  });

  it('never lets the page address out through the Referer header', async () => {
    const router = await started();

    await visit(router, '/b/Zx81bQ2token');
    await visit(router, '/fr/u/Faker');
    await visit(router, '/fr/champions?q=ahri');

    expect(sentNames()).toEqual(['view', 'view', 'view']);
    for (const { init } of sent()) {
      expect(init.referrerPolicy).toBe('no-referrer');
    }
  });

  it('sends the referrer once, and the template as the title of a private address', async () => {
    page.referrer = 'https://www.reddit.com/r/leagueoflegends/comments/1';
    const router = await started();

    await visit(router, '/b/Zx81bQ2token');
    await visit(router, '/en/16.1.1/items/1036-long-sword');

    const [shared, item] = payloads();
    expect(shared).toMatchObject({ title: '/b/:token', referrer: 'https://www.reddit.com' });
    expect(shared['data']).toEqual({ version: 'current' });
    expect(item).toMatchObject({ referrer: '', data: { locale: 'en', version: '16.1.1' } });
  });

  it('hands Umami its cache token back, kept in memory only', async () => {
    const router = await started();

    await visit(router, '/fr/champions');
    await visit(router, '/fr/legal/privacy');

    const [first, second] = sent().map(({ init }) => init.headers as Record<string, string>);
    expect(first['x-umami-cache']).toBeUndefined();
    expect(second['x-umami-cache']).toBe('cache-1');
    expect(page.defaultView.localStorage.getItem('x-umami-cache')).toBeNull();
  });

  it('counts an outcome rendered in place as an error, not a view', async () => {
    const router = await started();

    await visit(router, '/fr/no/such/page');

    expect(payloads()).toEqual([
      expect.objectContaining({
        name: 'page-error',
        url: '/fr/**',
        data: { locale: 'fr', version: 'current', status: 404 },
      }),
    ]);
  });

  it('counts a search when it starts, never its text', async () => {
    const router = await started();

    await visit(router, '/fr/champions');
    await visit(router, '/fr/champions?q=a');
    await visit(router, '/fr/champions?q=ahri');
    await visit(router, '/fr/champions');
    await visit(router, '/fr/champions?q=zed');

    expect(sentNames()).toEqual(['view', 'search', 'search']);
    expect(JSON.stringify(payloads())).not.toMatch(/ahri|zed/);
  });

  it('counts a shared build from its page', async () => {
    const router = await started();

    await visit(router, '/b/Zx81bQ2token');
    TestBed.inject(AudienceTracker).track('build-share');
    await settle();

    expect(sentNames()).toEqual(['view', 'build-share']);
    expect(sentUrls()).toEqual(['/b/:token', '/b/:token']);
  });

  it.each(['/fr/account/reset-password/a-secret-token', '/admin/users'])(
    'sends nothing from %s, not even an event',
    async (url) => {
      const router = await started();

      await visit(router, url);
      TestBed.inject(AudienceTracker).track('build-share');
      await settle();

      expect(fetchMock).not.toHaveBeenCalled();
    },
  );

  it.each(['test.league-of-data-base.com', 'localhost', 'league-of-data-base.fr'])(
    'stays silent on %s',
    async (hostname) => {
      page.location.hostname = hostname;
      const router = await started();

      await visit(router, '/fr/champions');

      expect(fetchMock).not.toHaveBeenCalled();
    },
  );

  it('stays silent once the visitor opts out', async () => {
    const router = await started();
    TestBed.inject(AudienceOptOut).choose(false);

    await visit(router, '/fr/champions');

    expect(fetchMock).not.toHaveBeenCalled();
    expect(page.defaultView.localStorage.getItem('lodb.audience.opt-out')).toBe('1');
  });

  it('stays silent while the browser sends Global Privacy Control', async () => {
    page.defaultView.navigator.globalPrivacyControl = true;
    const router = await started();
    TestBed.inject(AudienceOptOut).choose(true);

    await visit(router, '/fr/champions');

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([
    ['android', 'browser'],
    ['desktop', 'browser'],
    ['web', 'server'],
  ] as const)('stays silent on %s, %s', async (kind, platformId) => {
    const router = await started(kind, platformId);

    await visit(router, '/fr/champions');

    expect(fetchMock).not.toHaveBeenCalled();
  });
});
