import indexHtml from '../../../index.html' with { loader: 'text' };
import { themeFromCookie } from './theme-from-cookie';
import { THEMES } from './themes';

/** Runs the inline script of src/index.html against a stand-in document. */
function themeSetByInlineScript(cookie: string | (() => string)): string | undefined {
  const page = new DOMParser().parseFromString(indexHtml, 'text/html');
  const script = page.querySelector('head > script:not([src])')?.textContent ?? '';
  const attributes = new Map<string, string>();
  const stub = {
    get cookie() {
      return typeof cookie === 'string' ? cookie : cookie();
    },
    documentElement: {
      setAttribute: (name: string, value: string) => attributes.set(name, value),
    },
  };

  new Function('document', script)(stub);
  return attributes.get('data-theme');
}

const CASES: [cookie: string, expected: string][] = [
  ['', 'hextech'],
  ['lod_theme=zaun', 'zaun'],
  ['lod_theme=noxus', 'noxus'],
  ['lod_theme=spirit-blossom', 'spirit-blossom'],
  ['lod_theme=hextech', 'hextech'],
  ['lod_prefs=abc; lod_theme=noxus; other=1', 'noxus'],
  ['other=1;lod_theme=zaun', 'zaun'],
  ['lod_theme=demacia', 'hextech'],
  ['lod_theme=Zaun', 'hextech'],
  ['lod_theme=', 'hextech'],
  ['lod_theme=zaun%00', 'hextech'],
  ['xlod_theme=zaun', 'hextech'],
  ['lod_theme_old=zaun', 'hextech'],
  ['lod_theme=demacia; lod_theme=zaun', 'hextech'],
];

describe('themeFromCookie', () => {
  it.each(CASES)('reads %j as %s', (cookie, expected) => {
    expect(themeFromCookie(cookie)).toBe(expected);
  });

  it('knows the four identities, hextech first as the default', () => {
    expect(THEMES).toEqual(['hextech', 'zaun', 'noxus', 'spirit-blossom']);
  });
});

describe('inline theme script of index.html', () => {
  it.each(CASES)('sets data-theme like themeFromCookie for %j', (cookie) => {
    expect(themeSetByInlineScript(cookie)).toBe(themeFromCookie(cookie));
  });

  it('falls back to the default when the cookie cannot be read', () => {
    const sandboxed = () => {
      throw new DOMException('Cookies are disabled in this document', 'SecurityError');
    };

    expect(themeSetByInlineScript(sandboxed)).toBe('hextech');
  });
});
