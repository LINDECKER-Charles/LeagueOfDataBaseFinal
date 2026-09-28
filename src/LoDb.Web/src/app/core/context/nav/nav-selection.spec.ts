import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import type { Locale } from '../../i18n/locales';
import type { Preferences } from '../preferences/preferences';
import type { NavSelection } from './nav-selection';
import { navSelectionOf } from './nav-selection-of';

const LATEST = '16.19.1';
const OLDER = '14.1.1';
const META: CatalogMeta = {
  latest: LATEST,
  versions: [LATEST, '16.18.1', OLDER],
  readyVersions: [LATEST],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US', 'en_GB', 'fr_FR', 'ja_JP'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
  ],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};

interface Case {
  readonly rule: string;
  readonly url: string;
  readonly locale?: Locale;
  readonly remembered?: Preferences;
  readonly expected: NavSelection;
}

const CASES: Case[] = [
  {
    rule: 'follows the latest version in the own language',
    url: '/en/champions',
    expected: { shown: LATEST, version: null, lang: null },
  },
  {
    rule: 'keeps a version pinned in the path, on a detail too',
    url: `/en/${OLDER}/champions/Ahri`,
    expected: { shown: OLDER, version: OLDER, lang: null },
  },
  {
    rule: 'keeps the version the query of another page names',
    url: `/en/about?version=${OLDER}`,
    expected: { shown: OLDER, version: OLDER, lang: null },
  },
  {
    rule: 'keeps a regional variant of the locale',
    url: '/en/items?lang=en_GB&page=2',
    expected: { shown: LATEST, version: null, lang: 'en_GB' },
  },
  {
    rule: "drops another language's code and the own one",
    url: '/fr/runes?lang=ja_JP',
    expected: { shown: LATEST, version: null, lang: null },
  },
  {
    rule: 'drops a version the catalogue does not list',
    url: '/en/?version=1.0.0',
    expected: { shown: LATEST, version: null, lang: null },
  },
  {
    rule: 'fills what the URL leaves unsaid with the remembered choice',
    url: '/en/trends',
    remembered: { lang: 'en_GB', version: OLDER },
    expected: { shown: OLDER, version: OLDER, lang: 'en_GB' },
  },
  {
    rule: 'lets the URL win over the remembered choice',
    url: '/en/16.18.1/items?lang=en_US',
    remembered: { lang: 'en_GB', version: OLDER },
    expected: { shown: '16.18.1', version: '16.18.1', lang: null },
  },
  {
    rule: 'reads a page outside the locales in the locale it speaks',
    url: `/b/abc123?version=${OLDER}`,
    locale: 'fr',
    expected: { shown: OLDER, version: OLDER, lang: null },
  },
];

describe('navSelectionOf', () => {
  it.each(CASES)('$rule', ({ url, locale, remembered, expected }) => {
    const page = { url, locale: locale ?? 'en' };

    expect(navSelectionOf(page, META, remembered ?? null)).toEqual(expected);
  });

  it('names nothing while no version is ingested', () => {
    const empty = { ...META, latest: null, versions: [] };

    expect(navSelectionOf({ url: '/en/', locale: 'en' }, empty, null)).toBeNull();
  });
});
