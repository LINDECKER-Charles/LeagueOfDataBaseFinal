import { safeReturnUrl } from './safe-return-url';

const ORIGIN = 'https://league-of-data-base.com';
const FALLBACK = '/fr/';

describe('safeReturnUrl', () => {
  it.each([
    ['/fr/account/builds', '/fr/account/builds'],
    ['/fr/items?tag=Boots%2CArmor#list', '/fr/items?tag=Boots%2CArmor#list'],
    [`${ORIGIN}/en/champions/Aatrox`, '/en/champions/Aatrox'],
    ['fr/faq', '/fr/faq'],
    ['  /fr/about', '/fr/about'],
  ])('keeps %j on the same host: %j', (candidate, expected) => {
    expect(safeReturnUrl(candidate, ORIGIN, FALLBACK)).toBe(expected);
  });

  it.each([
    'https://evil.example/fr/',
    '//evil.example/fr/',
    '/\\evil.example/fr/',
    '\\\\evil.example',
    '/\t/evil.example',
    `${ORIGIN}//evil.example`,
    `${ORIGIN}:8443/fr/`,
    'http://league-of-data-base.com/fr/',
    'https://league-of-data-base.com.evil.example/',
    'https://evil.example@league-of-data-base.com.evil.example/',
    'javascript:alert(document.cookie)',
    'data:text/html,<script>alert(1)</script>',
    'mailto:someone@example.com',
    '',
    null,
    undefined,
  ])('falls back for %j', (candidate) => {
    expect(safeReturnUrl(candidate, ORIGIN, FALLBACK)).toBe(FALLBACK);
  });
});
