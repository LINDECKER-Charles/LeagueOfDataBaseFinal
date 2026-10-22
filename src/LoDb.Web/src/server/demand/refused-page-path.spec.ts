import { refusedPagePathOf } from './refused-page-path';

describe('refusedPagePathOf', () => {
  it.each([
    ['/fr/15.13.1/champions', '/fr/15.13.1/champions'],
    ['/fr/15.13.1/items/1036-long-sword', '/fr/15.13.1/items/1036-long-sword'],
    ['/fr/15.13.1/changes', '/fr/15.13.1/changes'],
    [
      '/zh-hans/0.151.101/summoners/SummonerFlash_Jade',
      '/zh-hans/0.151.101/summoners/SummonerFlash_Jade',
    ],
  ])('keeps the page of an older patch %s', (url, path) => {
    expect(refusedPagePathOf(url)).toBe(path);
  });

  it.each([
    ['its query', '/fr/15.13.1/items?sort=gold', '/fr/15.13.1/items'],
    ['its fragment', '/fr/15.13.1/runes#8000', '/fr/15.13.1/runes'],
    ['a trailing slash', '/fr/15.13.1/champions/Aatrox/', '/fr/15.13.1/champions/Aatrox'],
    [
      'escapes the router decodes',
      '/%66r/%31%35.13.1/champions/%41atrox',
      '/fr/15.13.1/champions/Aatrox',
    ],
    ['doubled slashes', '/fr//15.13.1//champions', '/fr/15.13.1/champions'],
  ])('drops %s, so that every spelling counts once', (_what, url, path) => {
    expect(refusedPagePathOf(url)).toBe(path);
  });

  it.each([
    ['a page of the latest patch', '/fr/champions/Aatrox'],
    ['the home of a locale', '/fr/'],
    ['a patch without a page', '/fr/15.13.1/'],
    ['a version only by its first digit', '/fr/1abc/champions'],
    ['an unknown locale', '/xx/15.13.1/champions'],
    ['an unknown kind of page', '/fr/15.13.1/patches/notes'],
    ['a page below an entity', '/fr/15.13.1/items/1036/extra'],
    ['a page below the patch page', '/fr/15.13.1/changes/items'],
    ['a malformed escape', '/fr/15.13.1/items/%E0%A4%A'],
    ['a segment outside the ids and slugs', '/fr/15.13.1/champions/%3Cscript%3E'],
    ['the latest page by query', '/fr/champions?version=15.13.1'],
    ['an endless slug', `/fr/15.13.1/items/1036-${'a'.repeat(200)}`],
  ])('ignores %s', (_what, url) => {
    expect(refusedPagePathOf(url)).toBeNull();
  });
});
