import type { Request, Response } from 'express';
import { championFacets } from '../../app/features/catalogue/champions/list/champion-facets';
import { itemFacetsOf } from '../../app/features/catalogue/items/list/facets/item-facets-of';
import { runeFacetsOf } from '../../app/features/catalogue/runes/list/rune-facets-of';
import type { FacetDefinition } from '../../app/features/catalogue/shared/facets/model/facet-definition';
import { summonerFacetsOf } from '../../app/features/catalogue/summoners/list/summoner-facets-of';
import { ownedParamsOf } from './owned-params-of';
import { preRouteOf } from './pre-route-of';
import { preRouter } from './pre-router';

const LIMITS = { maxParams: 16, maxParamLength: 200 };
const route = (url: string) => preRouteOf(url, LIMITS);

interface Answer {
  status?: number;
  headers: Record<string, string>;
  body?: string;
  isNextCalled: boolean;
}

// Runs the middleware on a URL; `isNextCalled` tells that the request went on to the render.
function serve(url: string, onRedirect?: () => void): Answer {
  const answer: Answer = { headers: {}, isNextCalled: false };
  const response = {
    set(fields: string | Record<string, string>, value?: string) {
      Object.assign(answer.headers, typeof fields === 'string' ? { [fields]: value } : fields);
      return response;
    },
    location(location: string) {
      answer.headers['Location'] = location;
      return response;
    },
    status(code: number) {
      answer.status = code;
      return response;
    },
    type(type: string) {
      answer.headers['Content-Type'] = type;
      return response;
    },
    send(body: string) {
      answer.body = body;
      return response;
    },
    end: () => response,
  };
  const request = { originalUrl: url } as Request;
  preRouter(LIMITS, onRedirect)(
    request,
    response as unknown as Response,
    () => (answer.isNextCalled = true),
  );
  return answer;
}

describe('preRouter', () => {
  // `/metrics` included: the scrape is served on its own port only (src/server/metrics).
  it.each([
    '/wp-login.php',
    '/wordpress/wp-admin/',
    '/champions/Ahri',
    '/9.3.1/champions',
    '/metrics',
  ])('answers %s with the static 404, without Angular', (url) => {
    const answer = serve(url);

    expect(answer.isNextCalled).toBe(false);
    expect(answer.status).toBe(404);
    expect(answer.headers).toMatchObject({
      'Cache-Control': 'public, max-age=0, s-maxage=3600',
      'X-Robots-Tag': 'noindex',
      'Content-Type': 'html',
    });
    expect(answer.headers['Content-Security-Policy']).toContain("script-src 'self'");
    expect(answer.headers['Reporting-Endpoints']).toBe('csp="/api/csp-reports"');
    expect(answer.body).toContain('404');
    expect(answer.body).not.toContain('<script');
  });

  it('redirects a detail with an unknown parameter, out of every cache', () => {
    expect(serve('/en/items/3031-infinity-edge?x=1')).toEqual({
      status: 301,
      headers: { 'Cache-Control': 'no-store', Location: '/en/items/3031-infinity-edge' },
      isNextCalled: false,
    });
  });

  it('tells each redirect it answers, and nothing else', () => {
    const onRedirect = vi.fn();

    serve('/en/items/3031-infinity-edge?x=1', onRedirect);
    serve('/wp-login.php', onRedirect);
    serve('/fr%2Fchampions', onRedirect);
    serve('/en/champions', onRedirect);

    expect(onRedirect).toHaveBeenCalledOnce();
  });

  it.each(['/fr%2Fchampions', '/fr/x/../15.13.1/champions'])(
    'refuses %s with a 400, never stored',
    (url) => {
      const answer = serve(url);

      expect(answer).toMatchObject({ status: 400, isNextCalled: false });
      expect(answer.headers['Cache-Control']).toBe('no-store');
    },
  );

  it.each(['/en/champions', '/en/champions?version=15.14.1', '/b/Zx81?x=1', '/xx/about?x=1'])(
    'lets %s render',
    (url) => {
      expect(serve(url)).toEqual({ headers: {}, isNextCalled: true });
    },
  );
});

describe('preRouteOf', () => {
  it.each([
    '/fr%2Fchampions',
    '/fr/champions%2F..%2Fabout',
    '/fr/%00/champions',
    '/fr/items/%E0%A4%A',
  ])('refuses %s', (url) => {
    expect(route(url)).toEqual({ kind: 'bad-request' });
  });

  // The renderer resolves dot segments and backslashes as a browser does, and its router reads
  // outlets and matrix parameters: each of these would render 15.13.1 in the current lane.
  it.each([
    '/fr/x/../15.13.1/champions',
    '/fr/./15.13.1/champions',
    '/fr/x/%2e%2E/15.13.1/champions/103-ahri',
    '/fr/x\\..\\15.13.1/champions',
    '/fr/(15.13.1/champions)',
    '/fr/champions;version=15.13.1',
    '/fr/champions/103-ahri;version=15.13.1?x=1',
    '/b/../fr/15.13.1/items',
  ])('refuses %s, which the renderer would read as another page', (url) => {
    expect(route(url)).toEqual({ kind: 'bad-request' });
  });

  it.each(['/./fr/15.13.1/champions', '/wp-login.php;jsessionid=1', '/(fr/15.13.1/champions)'])(
    'answers %s outside the grammar with the static 404',
    (url) => {
      expect(route(url)).toEqual({ kind: 'not-found' });
    },
  );

  it.each([
    '/wp-login.php',
    '/.env',
    '//evil.example/fr/champions?x=1',
    '/EN/champions',
    '/fra/champions',
    '/zh-hanx/champions',
    '/sitemap-old.xml',
    '/%E2%80%A6/champions',
  ])('answers %s with the static 404', (url) => {
    expect(route(url)).toEqual({ kind: 'not-found' });
  });

  it.each([
    '/fr/',
    '/zh-hant/champions',
    '/%66r/champions',
    '/b/Zx81',
    '/admin/users?range=7d',
    '/en/trends?mode=aram&page=2',
    '/en/account/verify-email?token=t0k3n',
    '/en/u/faker?tab=builds',
    '/en/about?x=1',
    '/xx/champions?x=1',
    '/en/champions/103-ahri/skins?x=1',
  ])('lets %s render as it is', (url) => {
    expect(route(url)).toEqual({ kind: 'render' });
  });

  it.each([
    '/en/champions?version=15.14.1&lang=en_GB',
    '/en/items?q=boots&page=2&size=all&tag=Boots,Armor&tag_all=1&price=0-3000',
    '/en/items?move_speed=0-100&move_speed_pct=0-10&attack_speed_pct=0-50&purchasable=1',
    '/en/champions?role=Mage&resource=Mana&range=ranged&difficulty=1-3&hp=0-700&as=0.6-0.7',
    '/en/runes?path=8100&slot=keystone',
    '/en/summoners?mode=CLASSIC&edition=modern&level=1&cooldown=0-300',
    '/fr/15.13.1/items/3031-infinity-edge?lang=fr_FR',
    '/en/items/3031-infinity-edge?version=15.14.1',
    '/en/items?la%6Eg=en_GB',
  ])('keeps the parameters %s reads', (url) => {
    expect(route(url)).toEqual({ kind: 'render' });
  });

  it.each([
    ['/en/champions?x=1', '/en/champions'],
    ['/en/champions?', '/en/champions'],
    ['/en/champions?&&q=ahri', '/en/champions?q=ahri'],
    ['/en/items?tag=Boots&utm_source=mail&page=2', '/en/items?tag=Boots&page=2'],
    ['/en/items/1036?q=sword&version=15.14.1', '/en/items/1036?version=15.14.1'],
    ['/en/runes?path=8100&path=8000', '/en/runes?path=8100'],
    ['/en/runes?tag=Boots', '/en/runes'],
    ['/fr/%31%35.13.1/champions/Ahri?x=1', '/fr/%31%35.13.1/champions/Ahri'],
    ['/fr/%63hampions?x=1', '/fr/%63hampions'],
    [`/en/champions?q=${'a'.repeat(199)}&page=2`, '/en/champions?page=2'],
    ['/fr/patches?x=1', '/fr/patches'],
    ['/fr/patches?version=15.14.1&lang=fr_FR', '/fr/patches'],
  ])('redirects %s to %s', (url, location) => {
    expect(route(url)).toEqual({ kind: 'redirect', location });
  });

  it('keeps the first parameters a list reads, within the bound', () => {
    const facets = ['role', 'resource', 'range', 'difficulty', 'attack', 'defense', 'magic'];
    const stats = ['hp', 'armor', 'mr', 'ad', 'as', 'ms', 'q', 'page', 'size', 'lang'];
    const query = [...facets, ...stats].map((name) => `${name}=1`);

    const cleaned = route(`/en/champions?${query.join('&')}`);

    expect(cleaned).toEqual({
      kind: 'redirect',
      location: `/en/champions?${query.slice(0, 16).join('&')}`,
    });
  });
});

// The server lets a list keep the parameters its facets read, from constants the schemas
// share: a facet keyed elsewhere would lose its filter at the next reload.
describe('ownedParamsOf, against the facet schemas', () => {
  const translate = (key: string) => key;
  const schemas: [string, FacetDefinition[]][] = [
    ['champions', championFacets(translate, [])],
    ['items', itemFacetsOf([], translate)],
    ['runes', runeFacetsOf([], translate)],
    ['summoners', summonerFacetsOf({ levels: [], modes: [] }, [], translate)],
  ];

  it.each(schemas)('lets the %s list keep each of its facets', (resource, schema) => {
    const owned = ownedParamsOf([resource]);
    const names = schema.flatMap((facet) =>
      facet.matchAll ? [facet.key, `${facet.key}_all`] : [facet.key],
    );

    expect(names.filter((name) => !owned?.has(name))).toEqual([]);
  });

  it('lets a detail keep its version and language only', () => {
    expect([...(ownedParamsOf(['items', '1036']) ?? [])].sort()).toEqual(['lang', 'version']);
  });
});
