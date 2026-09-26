import { alternatesOf } from './alternates-of';
import { hreflangOf } from './hreflang-of';
import { seoUrlsOf } from './seo-urls-of';

const ORIGIN = 'https://league-of-data-base.com';

describe('seoUrlsOf', () => {
  it('builds the canonical from the address, the home with its trailing slash', () => {
    expect(seoUrlsOf({ origin: ORIGIN, locale: 'fr', version: null, path: '' }).canonical).toBe(
      `${ORIGIN}/fr/`,
    );
    expect(
      seoUrlsOf({ origin: ORIGIN, locale: 'fr', version: null, path: 'items/1004-faerie-charm' })
        .canonical,
    ).toBe(`${ORIGIN}/fr/items/1004-faerie-charm`);
  });

  it('keeps a pinned version in every page of the address', () => {
    const urls = seoUrlsOf({ origin: ORIGIN, locale: 'de', version: '16.18.1', path: 'runes' });

    expect(urls.canonical).toBe(`${ORIGIN}/de/16.18.1/runes`);
    expect(urls.page('/runes/8000-precision')).toBe(`${ORIGIN}/de/16.18.1/runes/8000-precision`);
  });

  it('makes root-relative URLs absolute and keeps absolute ones', () => {
    const urls = seoUrlsOf({ origin: ORIGIN, locale: 'en', version: null, path: '' });

    expect(urls.absolute('/preview/items.png')).toBe(`${ORIGIN}/preview/items.png`);
    expect(urls.absolute('https://ddragon.example/Ahri_0.jpg')).toBe(
      'https://ddragon.example/Ahri_0.jpg',
    );
  });
});

describe('hreflangOf', () => {
  it('writes the script subtags in title case, as BCP 47 does', () => {
    expect(hreflangOf('zh-hans')).toBe('zh-Hans');
    expect(hreflangOf('zh-hant')).toBe('zh-Hant');
    expect(hreflangOf('pt')).toBe('pt');
  });
});

describe('alternatesOf', () => {
  it('lists the page in the 21 locales, then its en version as the default', () => {
    const alternates = alternatesOf({
      origin: ORIGIN,
      locale: 'fr',
      version: null,
      path: 'champions/Ahri',
    });

    expect(alternates).toHaveLength(22);
    expect(alternates).toContainEqual({
      hreflang: 'zh-Hant',
      href: `${ORIGIN}/zh-hant/champions/Ahri`,
    });
    expect(alternates.at(-1)).toEqual({
      hreflang: 'x-default',
      href: `${ORIGIN}/en/champions/Ahri`,
    });
  });

  it('points the home default to the root, which picks the visitor language', () => {
    const alternates = alternatesOf({ origin: ORIGIN, locale: 'ja', version: null, path: '' });

    expect(alternates.at(-1)).toEqual({ hreflang: 'x-default', href: `${ORIGIN}/` });
  });

  it('keeps a pinned version in every alternate', () => {
    const alternates = alternatesOf({
      origin: ORIGIN,
      locale: 'fr',
      version: '16.18.1',
      path: '',
    });

    expect(alternates.filter((alternate) => !alternate.href.includes('/16.18.1/'))).toEqual([]);
  });
});
