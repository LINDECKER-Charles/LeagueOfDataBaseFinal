import type { Preferences } from '../../../core/context/preferences/preferences';
import type { ContextTarget } from '../../../core/context/switch/context-target';
import { switchContext } from '../../../core/context/switch/switch-context';
import type { Locale } from '../../../core/i18n/locales';
import { catalogMetaOf } from '../../../core/testing/catalog-meta-of';
import type { LanguageOption } from '../options/language-option';
import { languageOptions } from '../options/language-options';
import { currentChoice } from './current-choice';
import { preferencesOf } from './preferences-of';
import { rememberedTarget } from './remembered-target';
import { targetOf } from './target-of';

const LATEST = '16.19.1';
const OLDER = '15.14.1';
const META = catalogMetaOf({
  latest: LATEST,
  versions: [LATEST, '16.18.1', OLDER],
  languages: ['en_US', 'en_GB', 'fr_FR', 'zh_CN', 'zh_TW', 'zh_MY'],
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
    { locale: 'zh-hans', language: 'zh_CN' },
    { locale: 'zh-hant', language: 'zh_TW' },
  ],
});
const OPTIONS = languageOptions(META);

function option(key: string): LanguageOption {
  const found = OPTIONS.find((candidate) => candidate.key === key);
  if (found === undefined) {
    throw new Error(`no option ${key}`);
  }
  return found;
}

describe('currentChoice', () => {
  interface Case {
    readonly rule: string;
    readonly url: string;
    readonly locale: Locale;
    readonly version: string;
    readonly language: string;
  }
  const cases: Case[] = [
    {
      rule: 'the latest version and the own language',
      url: '/fr/champions',
      locale: 'fr',
      version: LATEST,
      language: 'fr:fr_FR',
    },
    {
      rule: 'a version pinned in the path',
      url: `/fr/${OLDER}/items/1036-long-sword`,
      locale: 'fr',
      version: OLDER,
      language: 'fr:fr_FR',
    },
    {
      rule: 'a version and a variant in the query',
      url: `/en/?version=${OLDER}&lang=en_GB`,
      locale: 'en',
      version: OLDER,
      language: 'en:en_GB',
    },
    {
      rule: 'an unknown version as the latest',
      url: '/en/?version=1.0.0',
      locale: 'en',
      version: LATEST,
      language: 'en:en_US',
    },
    {
      rule: "another locale's language as the own",
      url: '/fr/?lang=en_GB',
      locale: 'fr',
      version: LATEST,
      language: 'fr:fr_FR',
    },
    {
      rule: 'a variant listed under a sibling as the own',
      url: '/zh-hant/?lang=zh_MY',
      locale: 'zh-hant',
      version: LATEST,
      language: 'zh-hant:zh_TW',
    },
    {
      rule: 'the locale spoken outside the locales',
      url: '/b/abc123',
      locale: 'fr',
      version: LATEST,
      language: 'fr:fr_FR',
    },
    {
      rule: 'the language named outside the locales, whichever locale owns it',
      url: '/b/abc123?lang=fr_FR',
      locale: 'en',
      version: LATEST,
      language: 'fr:fr_FR',
    },
  ];

  it.each(cases)('reads $rule', ({ url, locale, version, language }) => {
    const shown = { url, locale, remembered: null };

    expect(currentChoice(shown, META, OPTIONS)).toEqual({ version, language });
  });

  // The legacy `page_selection`: the session's patch on `/about` as on the catalogue. What a
  // path or a query names wins over it: pageContextOf's rule (ADR 0005), tested there.
  it('shows the kept context where neither the path nor the query names one', () => {
    const remembered: Preferences = { version: OLDER, lang: 'en_GB' };
    const shown = { url: '/en/about', locale: 'en' as const, remembered };

    expect(currentChoice(shown, META, OPTIONS)).toEqual({ version: OLDER, language: 'en:en_GB' });
  });

  it('shows nothing while no version is ingested', () => {
    const empty = { ...META, latest: null, versions: [] };
    const shown = { url: '/en/', locale: 'en' as const, remembered: null };

    expect(currentChoice(shown, empty, OPTIONS)).toBeNull();
  });
});

// How each target becomes a URL is switchContext's (core/context/switch): a choice only has
// to hand it the locale, the variant and the version it names.
describe('targetOf', () => {
  it('asks for the locale, the variant and the version of the choice', () => {
    const target = targetOf(OLDER, option('en:en_GB'));

    expect(switchContext('/fr/champions', target, META)).toBe(`/en/${OLDER}/champions?lang=en_GB`);
  });
});

describe('preferencesOf', () => {
  it('remembers an older version and a variant', () => {
    const target = targetOf(OLDER, option('en:en_GB'));

    expect(preferencesOf(target, META)).toEqual({ lang: 'en_GB', version: OLDER, locale: 'en' });
  });

  it('remembers the locale, and to follow the latest in its own language', () => {
    const target = targetOf(LATEST, option('fr:fr_FR'));

    expect(preferencesOf(target, META)).toEqual({ lang: null, version: null, locale: 'fr' });
  });
});

describe('rememberedTarget', () => {
  const REMEMBERED: Preferences = { lang: 'en_GB', version: OLDER };
  interface Case {
    readonly rule: string;
    readonly url: string;
    readonly remembered?: Preferences;
    readonly expected: ContextTarget | null;
  }
  const cases: Case[] = [
    {
      rule: 'fills both axes of a bare list',
      url: '/en/champions',
      expected: { version: OLDER, lang: 'en_GB' },
    },
    {
      rule: 'fills both axes of the home',
      url: '/en/',
      expected: { version: OLDER, lang: 'en_GB' },
    },
    {
      rule: 'leaves the version a path names',
      url: '/en/16.18.1/items',
      expected: { lang: 'en_GB' },
    },
    {
      rule: 'leaves the version a query names',
      url: '/en/?version=16.18.1',
      expected: { lang: 'en_GB' },
    },
    {
      rule: 'leaves the language a query names',
      url: '/en/items?lang=en_US',
      expected: { version: OLDER },
    },
    { rule: 'keeps a variant off another locale', url: '/fr/items', expected: { version: OLDER } },
    {
      rule: 'ignores a version no longer listed',
      url: '/fr/items',
      remembered: { lang: null, version: '1.0.0' },
      expected: null,
    },
    {
      rule: 'ignores the latest, already followed',
      url: '/fr/items',
      remembered: { lang: null, version: LATEST },
      expected: null,
    },
    {
      rule: 'ignores the own language',
      url: '/en/items',
      remembered: { lang: 'en_US', version: null },
      expected: null,
    },
    {
      rule: 'ignores a language the API lists nowhere',
      url: '/en/items',
      remembered: { lang: 'en_AU', version: null },
      expected: null,
    },
    {
      rule: "fills both axes of the page of the latest patch's changes, as a list",
      url: '/en/changes',
      expected: { version: OLDER, lang: 'en_GB' },
    },
    { rule: 'leaves the pages that read no context', url: '/en/about', expected: null },
    { rule: 'leaves the pages outside the locales', url: '/b/abc123', expected: null },
  ];

  it.each(cases)('$rule', ({ url, remembered, expected }) => {
    expect(rememberedTarget(url, remembered ?? REMEMBERED, META)).toEqual(expected);
  });

  it('rewrites the URL once: the rewritten URL names what the cookie gave', () => {
    const target = rememberedTarget('/en/champions?page=2', REMEMBERED, META);
    const next = switchContext('/en/champions?page=2', target ?? {}, META);

    expect(next).toBe(`/en/${OLDER}/champions?page=2&lang=en_GB`);
    expect(rememberedTarget(next, REMEMBERED, META)).toBeNull();
  });
});
