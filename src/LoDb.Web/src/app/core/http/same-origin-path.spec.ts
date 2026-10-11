import { sameOriginPath } from './same-origin-path';

describe('sameOriginPath', () => {
  const page = 'https://league-of-data-base.com';

  it("names the path of the page's own files, without their query", () => {
    expect(sameOriginPath(`${page}/i18n/fr.json`, page)).toBe('i18n/fr.json');
    expect(sameOriginPath(`${page}/i18n/fr.json?v=2`, page)).toBe('i18n/fr.json');
  });

  it('treats an explicit default port as the same origin', () => {
    expect(sameOriginPath('https://league-of-data-base.com:443/i18n/en.json', page)).toBe(
      'i18n/en.json',
    );
  });

  it.each([
    'http://api:8080/api/meta',
    'http://league-of-data-base.com/i18n/en.json',
    'https://api.league-of-data-base.com/v1/items',
    'https://league-of-data-base.com.evil.test/i18n/en.json',
    'i18n/en.json',
  ])('leaves %s to another origin', (url) => {
    expect(sameOriginPath(url, page)).toBeNull();
  });
});
