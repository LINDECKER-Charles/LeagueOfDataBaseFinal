import { rewriteToSelfOrigin } from './rewrite-to-self-origin';

describe('rewriteToSelfOrigin', () => {
  const page = 'https://league-of-data-base.com';
  const self = 'http://127.0.0.1:4000';
  const rewrite = (url: string) => rewriteToSelfOrigin(url, page, self);

  it('sends same-origin files to the SSR server itself', () => {
    expect(rewrite(`${page}/i18n/fr.json`)).toBe(`${self}/i18n/fr.json`);
    expect(rewrite(`${page}/i18n/fr.json?v=2`)).toBe(`${self}/i18n/fr.json?v=2`);
  });

  it('treats an explicit default port as the same origin', () => {
    expect(rewrite('https://league-of-data-base.com:443/i18n/en.json')).toBe(
      `${self}/i18n/en.json`,
    );
  });

  it.each([
    'http://api:8080/api/meta',
    'http://league-of-data-base.com/i18n/en.json',
    'https://api.league-of-data-base.com/v1/items',
    'https://league-of-data-base.com.evil.test/i18n/en.json',
    'i18n/en.json',
  ])('leaves %s alone', (url) => {
    expect(rewrite(url)).toBe(url);
  });
});
