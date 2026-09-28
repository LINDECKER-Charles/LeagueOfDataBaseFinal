import { formatPublicUrl } from './format-public-url';
import { isCatalogueRoute } from './is-catalogue-route';
import { parsePublicUrl } from './parse-public-url';

// Stands for the pattern of /api/meta, which the specs of core/api/meta cover.
const isVersion = (segment: string) => /^\d+(?:\.\d+)+$/.test(segment);

describe('URL grammar (ADR 0005)', () => {
  it.each([
    { url: '/', locale: null, version: null, page: [] },
    { url: '/fr/', locale: 'fr', version: null, page: [] },
    { url: '/fr', locale: 'fr', version: null, page: [] },
    { url: '/fr/champions', locale: 'fr', version: null, page: ['champions'] },
    { url: '/fr/champions/Aatrox', locale: 'fr', version: null, page: ['champions', 'Aatrox'] },
    {
      url: '/fr/15.14.1/champions/Aatrox',
      locale: 'fr',
      version: '15.14.1',
      page: ['champions', 'Aatrox'],
    },
    {
      url: '/zh-hant/items/1036-long-sword',
      locale: 'zh-hant',
      version: null,
      page: ['items', '1036-long-sword'],
    },
    {
      url: '/en/99.99.1/runes/8000-precision',
      locale: 'en',
      version: '99.99.1',
      page: ['runes', '8000-precision'],
    },
    { url: '/en/legal/cookies', locale: 'en', version: null, page: ['legal', 'cookies'] },
    {
      url: '/en/account/builds/12/edit',
      locale: 'en',
      version: null,
      page: ['account', 'builds', '12', 'edit'],
    },
    { url: '/b/Zx81', locale: null, version: null, page: ['b', 'Zx81'] },
    { url: '/admin/users', locale: null, version: null, page: ['admin', 'users'] },
    { url: '/EN/champions', locale: null, version: null, page: ['EN', 'champions'] },
    { url: '/15.14.1/champions', locale: null, version: null, page: ['15.14.1', 'champions'] },
    { url: '/fr/16/champions', locale: 'fr', version: null, page: ['16', 'champions'] },
  ])('cuts $url', ({ url, locale, version, page }) => {
    const parsed = parsePublicUrl(url, isVersion);

    expect(parsed.locale).toBe(locale);
    expect(parsed.version).toBe(version);
    expect(parsed.page).toEqual(page);
  });

  it('keeps the query and the fragment apart from the path', () => {
    const parsed = parsePublicUrl('/fr/items?tag=Boots%2CArmor&lang=fr_FR#list', isVersion);

    expect(parsed.page).toEqual(['items']);
    expect(parsed.query.get('tag')).toBe('Boots,Armor');
    expect(parsed.query.get('lang')).toBe('fr_FR');
    expect(parsed.fragment).toBe('#list');
  });

  it('reads a question mark after the fragment as part of the fragment', () => {
    const parsed = parsePublicUrl('/fr/faq#why?', isVersion);

    expect(parsed.query.toString()).toBe('');
    expect(parsed.fragment).toBe('#why?');
  });

  it.each([
    '/',
    '/fr/',
    '/fr',
    '/fr/15.14.1/champions/Aatrox',
    '/fr/items?tag=Boots%2CArmor&price=0-3000#list',
    '/b/Zx81',
    '/en/account/profile/',
  ])('writes %s back unchanged', (url) => {
    expect(formatPublicUrl(parsePublicUrl(url, isVersion))).toBe(url);
  });

  it.each([
    [['champions'], true],
    [['items', '1036-long-sword'], true],
    [['runes'], true],
    [['summoners', 'SummonerFlash'], true],
    [[], false],
    [['trends'], false],
    [['u', 'faker'], false],
    [['Champions'], false],
  ])('tells whether %j is a catalogue page', (page, expected) => {
    expect(isCatalogueRoute(page)).toBe(expected);
  });
});
