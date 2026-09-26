import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { Preferences } from './preferences';
import { preferencesCookieEntry } from './preferences-cookie-entry';
import { preferencesFromCookie } from './preferences-from-cookie';
import { PreferencesStore } from './preferences-store';

const REMEMBERED: Preferences = { lang: 'en_GB', version: '15.14.1' };

describe('preferencesFromCookie', () => {
  const cases: [string, string, Preferences | null][] = [
    ['no cookie at all', '', null],
    ['other cookies only', 'lod_theme=zaun; lod_prefs2=l=en_GB', null],
    ['both fields', 'lod_prefs=l=en_GB&v=15.14.1', REMEMBERED],
    ['among other cookies', 'lod_theme=zaun; lod_prefs=l=en_GB&v=15.14.1; x=1', REMEMBERED],
    ['a language only', 'lod_prefs=l=en_GB', { lang: 'en_GB', version: null }],
    ['a version only', 'lod_prefs=v=15.14.1', { lang: null, version: '15.14.1' }],
    ['an empty value', 'lod_prefs=', null],
    ['empty fields', 'lod_prefs=l=&v=', null],
    ['the signed legacy value', 'lod_prefs=eyJsIjoiZW5fR0IifQ.c2lnbmF0dXJl', null],
    ['markup in a field', 'lod_prefs=l=%3Cscript%3E&v=15.14.1', null],
    ['a path in a field', 'lod_prefs=l=en_GB&v=..%2F..%2Fadmin', null],
    ['an overlong field', `lod_prefs=l=${'a'.repeat(33)}`, null],
  ];

  it.each(cases)('reads %s', (_case, cookies, expected) => {
    expect(preferencesFromCookie(cookies)).toEqual(expected);
  });
});

describe('preferencesCookieEntry', () => {
  it('remembers both fields for a year, site-wide and first-party only', () => {
    expect(preferencesCookieEntry(REMEMBERED, false)).toBe(
      'lod_prefs=l=en_GB&v=15.14.1; path=/; max-age=31536000; samesite=lax',
    );
  });

  it('leaves a default field out and marks the cookie secure on https', () => {
    expect(preferencesCookieEntry({ lang: null, version: '15.14.1' }, true)).toBe(
      'lod_prefs=v=15.14.1; path=/; max-age=31536000; samesite=lax; secure',
    );
  });

  it('expires the cookie when there is nothing left to remember', () => {
    const forgotten = 'lod_prefs=; path=/; max-age=0; samesite=lax';

    expect(preferencesCookieEntry(null, false)).toBe(forgotten);
    expect(preferencesCookieEntry({ lang: null, version: null }, false)).toBe(forgotten);
  });

  it('writes what the reader reads back', () => {
    const entry = preferencesCookieEntry(REMEMBERED, false).split(';')[0];

    expect(preferencesFromCookie(entry)).toEqual(REMEMBERED);
  });
});

describe('PreferencesStore', () => {
  function store(platform: 'browser' | 'server' = 'browser'): PreferencesStore {
    TestBed.configureTestingModule({ providers: [{ provide: PLATFORM_ID, useValue: platform }] });
    return TestBed.inject(PreferencesStore);
  }

  afterEach(() => {
    vi.restoreAllMocks();
    document.cookie = 'lod_prefs=; path=/; max-age=0';
  });

  it('reads back what it remembered', () => {
    const preferences = store();

    preferences.remember(REMEMBERED);

    expect(preferences.read()).toEqual(REMEMBERED);
  });

  it('forgets on null', () => {
    const preferences = store();
    preferences.remember(REMEMBERED);

    preferences.remember(null);

    expect(preferences.read()).toBeNull();
  });

  it('answers nothing when the cookie cannot be read', () => {
    vi.spyOn(document, 'cookie', 'get').mockImplementation(() => {
      throw new DOMException('Cookies are disabled in this document', 'SecurityError');
    });

    expect(store().read()).toBeNull();
  });

  it('neither reads nor writes a cookie on the server', () => {
    document.cookie = 'lod_prefs=l=en_GB; path=/';
    const written = vi.spyOn(document, 'cookie', 'set');
    const preferences = store('server');

    preferences.remember(REMEMBERED);

    expect(preferences.read()).toBeNull();
    expect(written).not.toHaveBeenCalled();
  });
});
