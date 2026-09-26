import { DOCUMENT, REQUEST } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { RedirectFunction } from '@angular/router';
import { negotiateLocale } from './negotiate-locale';
import { parseAcceptLanguage } from './parse-accept-language';
import { redirectToPreferredLocale } from './redirect-to-preferred-locale';

const localeFor = (header: string | null) => negotiateLocale(parseAcceptLanguage(header));

describe('parseAcceptLanguage', () => {
  it.each([
    ['fr-FR,fr;q=0.9,en-US;q=0.8,en;q=0.7', ['fr-fr', 'fr', 'en-us', 'en']],
    ['en;q=0.5, de', ['de', 'en']],
    ['ja;q=0.8,ko;q=0.8,it', ['it', 'ja', 'ko']],
    ['fr;q=0,en', ['en']],
    ['*,es;q=0.1', ['es']],
    ['de;q=1.0, en ; q=0.500', ['de', 'en']],
    ['de;Q=0.4,en;level=1;q=0.6', ['en', 'de']],
    ['fr;q=2,en;q=-1,it;q=abc,es;q=0.1234,pt', ['pt']],
    ['123,en_US,,fr', ['fr']],
    ['', []],
  ])('reads %j as %j', (header, expected) => {
    expect(parseAcceptLanguage(header)).toEqual(expected);
  });

  it('reads nothing from a missing header', () => {
    expect(parseAcceptLanguage(null)).toEqual([]);
    expect(parseAcceptLanguage(undefined)).toEqual([]);
  });

  it('stops reading a forged header after a bounded number of ranges', () => {
    const header = `${Array.from({ length: 1000 }, () => 'xx').join(',')},fr`;

    expect(parseAcceptLanguage(header)).not.toContain('fr');
  });
});

describe('negotiateLocale', () => {
  it.each([
    ['fr-FR,fr;q=0.9,en;q=0.8', 'fr'],
    ['pt-BR,pt;q=0.9', 'pt'],
    ['en-GB', 'en'],
    ['es-MX,es;q=0.9', 'es'],
    ['nl-NL,nl;q=0.9,de;q=0.8', 'de'],
    ['nl,sv', 'en'],
    ['zh-CN,zh;q=0.9', 'zh-hans'],
    ['zh', 'zh-hans'],
    ['zh-SG', 'zh-hans'],
    ['zh-TW', 'zh-hant'],
    ['zh-HK,en;q=0.5', 'zh-hant'],
    ['zh-MO', 'zh-hant'],
    ['zh-Hant', 'zh-hant'],
    ['zh-Hant-CN', 'zh-hant'],
    ['zh-Hans-TW', 'zh-hans'],
    ['ar-SA', 'ar'],
    ['id-ID', 'id'],
    ['EL', 'el'],
    ['*', 'en'],
    ['', 'en'],
    [null, 'en'],
  ])('sends %j to /%s/', (header, expected) => {
    expect(localeFor(header)).toBe(expected);
  });
});

describe('redirectToPreferredLocale', () => {
  const redirect = () =>
    TestBed.runInInjectionContext(() =>
      redirectToPreferredLocale({} as Parameters<RedirectFunction>[0]),
    );

  it('reads Accept-Language while rendering on a server', () => {
    const request = new Request('http://localhost/', { headers: { 'Accept-Language': 'ko' } });
    TestBed.configureTestingModule({ providers: [{ provide: REQUEST, useValue: request }] });

    expect(redirect()).toBe('/ko/');
  });

  it("reads the browser's languages otherwise", () => {
    const document = { defaultView: { navigator: { languages: ['it-IT', 'en'] } } };
    TestBed.configureTestingModule({ providers: [{ provide: DOCUMENT, useValue: document }] });

    expect(redirect()).toBe('/it/');
  });
});
