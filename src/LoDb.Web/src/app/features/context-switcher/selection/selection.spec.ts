import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import type { Preferences } from '../../../core/context/preferences/preferences';
import type { ContextTarget } from '../../../core/context/switch/context-target';
import { switchContext } from '../../../core/context/switch/switch-context';
import type { Locale } from '../../../core/i18n/locales';
import type { LanguageOption } from '../options/language-option';
import { languageOptions } from '../options/language-options';
import { currentChoice } from './current-choice';
import { preferencesOf } from './preferences-of';
import { rememberedTarget } from './remembered-target';
import { targetOf } from './target-of';

const LATEST = '16.19.1';
const OLDER = '15.14.1';
const META: CatalogMeta = {
  latest: LATEST,
  versions: [LATEST, '16.18.1', OLDER],
  readyVersions: [LATEST],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US', 'en_GB', 'fr_FR', 'zh_CN', 'zh_TW', 'zh_MY'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
    { locale: 'zh-hans', language: 'zh_CN' },
    { locale: 'zh-hant', language: 'zh_TW' },
  ],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};
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
      rule: 'the path over the query',
      url: `/en/${OLDER}/champions?version=16.18.1`,
      locale: 'en',
      version: OLDER,
      language: 'en:en_US',
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
    expect(currentChoice({ url, locale }, META, OPTIONS)).toEqual({ version, language });
  });

  it('shows nothing while no version is ingested', () => {
    const empty = { ...META, latest: null, versions: [] };

    expect(currentChoice({ url: '/en/', locale: 'en' }, empty, OPTIONS)).toBeNull();
  });
});

describe('the target path of a choice', () => {
  interface Case {
    readonly rule: string;
    readonly from: string;
    readonly version: string;
    readonly language: string;
    readonly to: string;
  }
  const cases: Case[] = [
    {
      rule: 'pins an older version in a catalogue path',
      from: '/fr/champions',
      version: OLDER,
      language: 'fr:fr_FR',
      to: `/fr/${OLDER}/champions`,
    },
    {
      rule: 'returns to the short URL on the latest',
      from: `/fr/${OLDER}/champions/Aatrox`,
      version: LATEST,
      language: 'fr:fr_FR',
      to: '/fr/champions/Aatrox',
    },
    {
      rule: 'moves to another locale on the same page',
      from: '/fr/items/1036-long-sword',
      version: LATEST,
      language: 'en:en_US',
      to: '/en/items/1036-long-sword',
    },
    {
      rule: 'reads a variant through ?lang=',
      from: '/en/runes',
      version: LATEST,
      language: 'en:en_GB',
      to: '/en/runes?lang=en_GB',
    },
    {
      rule: 'drops ?lang= back on the own language',
      from: '/en/runes?lang=en_GB',
      version: LATEST,
      language: 'en:en_US',
      to: '/en/runes',
    },
    {
      rule: 'puts the version in the query of the home',
      from: '/en/',
      version: OLDER,
      language: 'en:en_GB',
      to: `/en/?lang=en_GB&version=${OLDER}`,
    },
    {
      rule: 'keeps the filters of a list',
      from: '/fr/items?page=2&tags=Boots',
      version: OLDER,
      language: 'fr:fr_FR',
      to: `/fr/${OLDER}/items?page=2&tags=Boots`,
    },
  ];

  it.each(cases)('$rule', ({ from, version, language, to }) => {
    expect(switchContext(from, targetOf(version, option(language)), META)).toBe(to);
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
