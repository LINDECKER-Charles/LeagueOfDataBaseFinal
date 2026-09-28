import type { RequestFacts } from './request-facts';
import { routeRequest } from './route-request';
import type { SwRoute } from './sw-route';

const ORIGIN = 'https://league-of-data-base.com';
const HTML = 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8';

function navigation(path: string, overrides: Partial<RequestFacts> = {}): RequestFacts {
  return {
    method: 'GET',
    url: `${ORIGIN}${path}`,
    mode: 'navigate',
    accept: HTML,
    hasRange: false,
    ...overrides,
  };
}

function subresource(url: string, accept = '*/*'): RequestFacts {
  return { method: 'GET', url, mode: 'cors', accept, hasRange: false };
}

describe('routeRequest', () => {
  it.each<[string, SwRoute]>([
    ['/en/', 'page'],
    ['/fr/champions', 'page'],
    ['/fr/champions/Ahri', 'page'],
    ['/en/14.1.1/items/3031-infinity-edge', 'page'],
    ['/en/about', 'page'],
    ['/en/u/someone', 'page'],
    ['/', 'page'],
    ['/en/accounting', 'page'],
    ['/builds', 'page'],
  ])('answers the navigation to %s as a page', (path, route) => {
    expect(routeRequest(navigation(path), ORIGIN)).toBe(route);
  });

  it.each([
    '/api/meta',
    '/api',
    '/admin',
    '/admin/users',
    '/en/account',
    '/en/account/login',
    '/zh-hant/account/builds/12/edit',
    '/b/Zx81kq',
    '/webhooks/stripe',
    '/v1/champions',
  ])('leaves %s to the browser', (path) => {
    expect(routeRequest(navigation(path), ORIGIN)).toBe('bypass');
  });

  it('treats a fetch asking for HTML as a page', () => {
    const fetchForHtml = navigation('/en/runes', { mode: 'cors', accept: 'text/html' });

    expect(routeRequest(fetchForHtml, ORIGIN)).toBe('page');
  });

  it.each<[string, SwRoute]>([
    [`${ORIGIN}/build/main-M3ZYENBU.js`, 'asset'],
    [`${ORIGIN}/build/media/beaufort-PL7H3MLA.woff2`, 'asset'],
    [`${ORIGIN}/fonts/BeaufortforLOL-Bold.woff2`, 'asset'],
    [`${ORIGIN}/cdn/blobs/ab/cd/abcdef.webp`, 'blob'],
    [`${ORIGIN}/i18n/fr.json`, 'bypass'],
    [`${ORIGIN}/manifest.webmanifest`, 'bypass'],
    [`${ORIGIN}/api/champions`, 'bypass'],
    ['https://ddragon.leagueoflegends.com/cdn/img/champion/splash/Ahri_0.jpg', 'bypass'],
    ['https://d28xe8vt774jo5.cloudfront.net/abilities/videos/0103/Q1.webm', 'bypass'],
  ])('routes the subresource %s to %s', (url, route) => {
    expect(routeRequest(subresource(url), ORIGIN)).toBe(route);
  });

  it.each(['POST', 'PUT', 'DELETE', 'HEAD'])('leaves a %s request to the browser', (method) => {
    expect(routeRequest(navigation('/en/', { method }), ORIGIN)).toBe('bypass');
  });

  it('leaves a Range request to the browser, even for a cached kind of file', () => {
    const range = { ...subresource(`${ORIGIN}/cdn/blobs/ab/cd/abcdef.webm`), hasRange: true };

    expect(routeRequest(range, ORIGIN)).toBe('bypass');
  });

  it('leaves an event stream to the browser', () => {
    const stream = subresource(`${ORIGIN}/en/champions`, 'text/event-stream');

    expect(routeRequest(stream, ORIGIN)).toBe('bypass');
  });

  it('never answers for another origin, even a page', () => {
    const elsewhere = { ...navigation('/en/'), url: 'https://example.com/en/' };

    expect(routeRequest(elsewhere, ORIGIN)).toBe('bypass');
  });
});
