import indexHtml from '../../../index.html' with { loader: 'text' };
import { themeFromCookie } from './theme-from-cookie';
import { THEME_IDENTITIES } from './theme-identities';
import { THEMES, type Theme } from './themes';

interface Painted {
  readonly theme: string | undefined;
  readonly browserColor: string | null;
  readonly preloaded: string[];
}

/** Runs the inline script of src/index.html in a document parsed from that same file. */
function paintedByInlineScript(cookie: string | (() => string)): Painted {
  const page = new DOMParser().parseFromString(indexHtml, 'text/html');
  const script = page.querySelector('head > script:not([src])')?.textContent ?? '';
  const preloadsBefore = page.head.querySelectorAll('link[rel=preload]').length;
  const stub = {
    get cookie() {
      return typeof cookie === 'string' ? cookie : cookie();
    },
    documentElement: page.documentElement,
    head: page.head,
    querySelector: (selector: string) => page.querySelector(selector),
    createElement: (name: string) => page.createElement(name),
  };

  new Function('document', script)(stub);
  const preloads = Array.from(page.head.querySelectorAll<HTMLLinkElement>('link[rel=preload]'));
  return {
    theme: page.documentElement.getAttribute('data-theme') ?? undefined,
    browserColor: page.querySelector('meta[name="theme-color"]')?.getAttribute('content') ?? null,
    preloaded: preloads.slice(preloadsBefore).map((link) => link.getAttribute('href') ?? ''),
  };
}

function themeSetByInlineScript(cookie: string | (() => string)): string | undefined {
  return paintedByInlineScript(cookie).theme;
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

  it.each(THEMES)('gives %s its browser colour and display face', (theme: Theme) => {
    const painted = paintedByInlineScript(`lod_theme=${theme}`);
    const identity = THEME_IDENTITIES[theme];

    expect(painted.browserColor).toBe(identity.browserColor);
    expect(painted.preloaded).toEqual(identity.displayFont === null ? [] : [identity.displayFont]);
  });
});
