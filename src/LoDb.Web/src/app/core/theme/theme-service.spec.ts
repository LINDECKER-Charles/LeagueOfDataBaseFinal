import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { themeCookieEntry } from './theme-cookie-entry';
import { ThemeService } from './theme-service';
import type { Theme } from './themes';

function service(platform: 'browser' | 'server' = 'browser'): ThemeService {
  TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: platform }] });
  return TestBed.inject(ThemeService);
}

function browserColor(): string | null | undefined {
  return document.head.querySelector('meta[name="theme-color"]')?.getAttribute('content');
}

describe('ThemeService', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    document.cookie = 'lod_theme=; path=/; max-age=0';
    document.documentElement.removeAttribute('data-theme');
    document.head.querySelector('meta[name="theme-color"]')?.remove();
  });

  it('reads the identity the inline script painted from the cookie', () => {
    document.cookie = 'lod_theme=zaun; path=/';

    expect(service().current()).toBe('zaun');
    expect(browserColor()).toBe('#030706');
  });

  it('answers the default for an unknown cookie value', () => {
    document.cookie = 'lod_theme=demacia; path=/';

    expect(service().current()).toBe('hextech');
  });

  it('answers the default when the cookie cannot be read', () => {
    vi.spyOn(document, 'cookie', 'get').mockImplementation(() => {
      throw new DOMException('Cookies are disabled in this document', 'SecurityError');
    });

    expect(service().current()).toBe('hextech');
  });

  it('never reads the cookie on the server, so the HTML never depends on the theme', () => {
    document.cookie = 'lod_theme=noxus; path=/';

    expect(service('server').current()).toBe('hextech');
    expect(browserColor()).toBe('#010a13');
  });

  it('paints, remembers and recolours the browser chrome on select', () => {
    const written = vi.spyOn(document, 'cookie', 'set');
    const themes = service();

    themes.select('spirit-blossom');

    expect(themes.current()).toBe('spirit-blossom');
    expect(document.documentElement.getAttribute('data-theme')).toBe('spirit-blossom');
    expect(written).toHaveBeenCalledWith(
      'lod_theme=spirit-blossom; path=/; max-age=31536000; samesite=lax',
    );
    expect(browserColor()).toBe('#07070e');
  });

  it('ignores a value outside the list', () => {
    const written = vi.spyOn(document, 'cookie', 'set');
    const themes = service();

    themes.select('demacia' as Theme);

    expect(themes.current()).toBe('hextech');
    expect(document.documentElement.hasAttribute('data-theme')).toBe(false);
    expect(written).not.toHaveBeenCalled();
  });

  it("pins an identity over the visitor's own, then gives theirs back", () => {
    document.cookie = 'lod_theme=noxus; path=/';
    const themes = service();
    const written = vi.spyOn(document, 'cookie', 'set');

    themes.pin('hextech');
    expect(document.documentElement.getAttribute('data-theme')).toBe('hextech');
    expect(browserColor()).toBe('#010a13');
    expect(themes.current()).toBe('noxus');

    themes.unpin();
    expect(document.documentElement.getAttribute('data-theme')).toBe('noxus');
    expect(browserColor()).toBe('#08090b');
    expect(written).not.toHaveBeenCalled();
  });
});

describe('themeCookieEntry', () => {
  it('keeps the choice a year, site-wide and first-party only', () => {
    expect(themeCookieEntry('noxus', false)).toBe(
      'lod_theme=noxus; path=/; max-age=31536000; samesite=lax',
    );
  });

  it('is Secure whenever the page is', () => {
    expect(themeCookieEntry('zaun', true)).toBe(
      'lod_theme=zaun; path=/; max-age=31536000; samesite=lax; secure',
    );
  });
});
